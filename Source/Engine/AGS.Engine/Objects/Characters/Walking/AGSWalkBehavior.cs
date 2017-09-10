﻿using System;
using AGS.API;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace AGS.Engine
{
	public class AGSWalkBehavior : AGSComponent, IWalkBehavior
	{
        private TaskCompletionSource<object> _walkCompleted;
        private WalkLineInstruction _currentWalkLine;
        private IPathFinder _pathFinder;
		private List<IObject> _debugPath;
		private IObject _obj;
		private IFaceDirectionBehavior _faceDirection;
		private IHasOutfit _outfit;
		private IObjectFactory _objFactory;
		private ICutscene _cutscene;
		private IGameState _state;
        private IGameEvents _events;
        private IAnimationContainer _animation;
        private ISprite _lastFrame;
        private float _lastViewportX, _lastViewportY, _compensateScrollX, _compensateScrollY;
        private readonly IGLUtils _glUtils;

		public AGSWalkBehavior(IObject obj, IPathFinder pathFinder, IFaceDirectionBehavior faceDirection, 
                               IHasOutfit outfit, IObjectFactory objFactory, IGame game, IGLUtils glUtils)
		{
            _state = game.State;
            _cutscene = _state.Cutscene;
            _events = game.Events;
			_obj = obj;
			_pathFinder = pathFinder;
			_faceDirection = faceDirection;
			_outfit = outfit;
			_objFactory = objFactory;
            _glUtils = glUtils;

			_debugPath = new List<IObject> ();
			_walkCompleted = new TaskCompletionSource<object> ();
			_walkCompleted.SetResult(null);
            AdjustWalkSpeedToScaleArea = true;
            MovementLinkedToAnimation = true;
            WalkStep = new PointF(8f, 8f);

            _events.OnRepeatedlyExecute.Subscribe(onRepeatedlyExecute);
		}

        public static int WalkLineTimeoutInMilliseconds = 15000; //15 seconds

        public override void Dispose()
        {
            base.Dispose();
            _events.OnRepeatedlyExecute.Unsubscribe(onRepeatedlyExecute);
        }

        public override void Init(IEntity entity)
        {
            base.Init(entity);
            entity.Bind<IAnimationContainer>(c => _animation = c, _ => _animation = null);
        }

        #region IWalkBehavior implementation

		public async Task<bool> WalkAsync (ILocation location)	
		{
            WalkDestination = location;
			List<IObject> debugRenderers = _debugPath;
			if (debugRenderers != null) 
			{
				foreach (var renderer in debugRenderers) 
				{
					await renderer.ChangeRoomAsync(null);
				}
			}
			CancellationTokenSource token = await stopWalkingAsync();
			debugRenderers = DebugDrawWalkPath ? new List<IObject> () : null;
			_debugPath = debugRenderers;
			_walkCompleted = new TaskCompletionSource<object> (null);
			float xSource = _obj.X;
			float ySource = _obj.Y;
			bool completedWalk = false;
			Exception caught = null;
			try
			{
				completedWalk = await walkAsync(location, token, debugRenderers);
			}
			catch (Exception e)
			{
				caught = e;
			}

			//On windows (assuming we're before c# 6.0), we can't await inside a finally, so we're using the workaround pattern
            _faceDirection.CurrentDirectionalAnimation = _outfit.Outfit[AGSOutfit.Idle];
			await _faceDirection.FaceDirectionAsync(_faceDirection.Direction);
			_walkCompleted.TrySetResult(null);

			if (caught != null) throw caught;

			return completedWalk;
		}

		public void StopWalking()
		{
			Task.Run(async () => await StopWalkingAsync()).Wait();
		}

		public async Task StopWalkingAsync()
		{
			await stopWalkingAsync();
		}

		public void PlaceOnWalkableArea()
		{
			PointF current = new PointF (_obj.X, _obj.Y);
			PointF? closestPoint = getClosestWalkablePoint (current);
			if (closestPoint != null) 
			{
				_obj.X = closestPoint.Value.X;
				_obj.Y = closestPoint.Value.Y;
			}
		}

        public PointF WalkStep { get; set; }

        public bool AdjustWalkSpeedToScaleArea { get; set; }

        public bool MovementLinkedToAnimation { get; set; }
        
		public bool IsWalking
		{ 
			get
			{ 
				Task task = _walkCompleted.Task;
				return (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted);
			}
		}

        public ILocation WalkDestination { get; private set; }

		public bool DebugDrawWalkPath { get; set; }

        #endregion

        private void onRepeatedlyExecute()
        {
            WalkLineInstruction currentLine = _currentWalkLine;
            if (currentLine == null) return;

            if (currentLine.CancelToken.IsCancellationRequested || currentLine.NumSteps <= 1f || !isWalkable(_obj.Location))
            {
                _currentWalkLine = null; //Possible race condition here? If so, need to replace with concurrent queue
                _lastFrame = null;
                _compensateScrollX = _compensateScrollY = 0f;
                currentLine.OnCompletion.TrySetResult(null);                
                return;
            }
            if (_cutscene.IsSkipping)
            {
                _obj.X = currentLine.Destination.X;
                _obj.Y = currentLine.Destination.Y;

                _currentWalkLine = null; //Possible race condition here? If so, need to replace with concurrent queue
                _lastFrame = null;
                _compensateScrollX = _compensateScrollY = 0f;
                currentLine.OnCompletion.TrySetResult(null);
                return;
            }
            PointF walkSpeed = adjustWalkSpeed(WalkStep);
            float xStep = currentLine.XStep * walkSpeed.X;
            float yStep = currentLine.YStep * walkSpeed.Y;
            if (MovementLinkedToAnimation && _animation != null && _animation.Animation.Frames.Count > 1 && 
                _animation.Animation.Sprite == _lastFrame)
            {
                //If the movement is linked to the animation and the animation speed is slower the the viewport movement, it can lead to flickering
                //so we do a smooth movement for this scenario.
                _obj.X += compensateForViewScrollIfNeeded(_state.Viewport.X, xStep, ref _compensateScrollX, ref _lastViewportX);
                _obj.Y += compensateForViewScrollIfNeeded(_state.Viewport.Y, yStep, ref _compensateScrollY, ref _lastViewportY);
                return;
            }
            if (_animation != null) _lastFrame = _animation.Animation.Sprite;
            _lastViewportX = _state.Viewport.X;

            currentLine.NumSteps -= Math.Abs(currentLine.IsBaseStepX ? xStep : yStep);
			if (currentLine.NumSteps >= 0f)
			{
                _obj.X += (xStep - _compensateScrollX);
                _obj.Y += (yStep - _compensateScrollY);
			}
            _compensateScrollX = _compensateScrollY = 0f;
        }

        private float compensateForViewScrollIfNeeded(float currentViewport, float step, ref float compensateStep, ref float lastViewport)
        {
            if (currentViewport == lastViewport) return 0f;
            float smoothStep = step / (_animation.Animation.Configuration.DelayBetweenFrames + _animation.Animation.Frames[_animation.Animation.State.CurrentFrame].Delay);
            compensateStep += smoothStep;
            lastViewport = currentViewport;
            return smoothStep;
        }

		private async Task<bool> walkAsync(ILocation location, CancellationTokenSource token, List<IObject> debugRenderers)
		{
			IEnumerable<ILocation> walkPoints = getWalkPoints (location);

			if (!walkPoints.Any ()) 
				return false;
			foreach (var point in walkPoints) 
			{
				if (point.X == _obj.X && point.Y == _obj.Y) continue;
                if (!await walkStraightLine(point, token, debugRenderers))
                    return false;
			}
			return true;
		}

		private async Task<CancellationTokenSource> stopWalkingAsync()
		{
            var currentLine = _currentWalkLine;
            if (currentLine != null)
            {
                currentLine.CancelToken.Cancel();
                await currentLine.OnCompletion.Task;
            }
			CancellationTokenSource token = new CancellationTokenSource ();
			await _walkCompleted.Task;
			return token;
		}

		private PointF? getClosestWalkablePoint(PointF target)
		{
			var points = getClosestWalkablePoints(target);
			if (points.Count == 0) return null;
			return points[0];
		}

		private List<PointF> getClosestWalkablePoints(PointF target)
		{
			List<Tuple<PointF, float>> points = new List<Tuple<PointF, float>> (_obj.Room.Areas.Count);
            foreach (IArea area in getWalkableAreas()) 
			{
				float distance;
				PointF? point = area.FindClosestPoint (target, out distance);
				if (point == null) continue;
				points.Add(new Tuple<PointF, float> (point.Value, distance));
			}
			return points.OrderBy(p => p.Item2).Select(p => p.Item1).ToList();
		}

		private IEnumerable<ILocation> getWalkPoints(ILocation destination)
		{
			if (!isWalkable(_obj.Location))
				return new List<ILocation> ();
			List<PointF> closestPoints = new List<PointF> (_obj.Room.Areas.Count + 1);
			if (isWalkable(destination))
			{
				closestPoints.Add(destination.XY);
			}

			closestPoints.AddRange(getClosestWalkablePoints (destination.XY));
			if (closestPoints.Count == 0)
				return new List<ILocation>();

            Point offset;
			bool[][] mask = getWalkableMask(out offset);
            var from = _obj.Location;
            from = new AGSLocation(from.X - offset.X, from.Y - offset.Y, from.Z);
			_pathFinder.Init(mask);
			foreach (var closest in closestPoints)
			{
                destination = new AGSLocation (closest.X - offset.X, closest.Y - offset.Y, destination.Z);
				var walkPoints = _pathFinder.GetWalkPoints(from, destination);
                if (walkPoints.Any()) return walkPoints.Select(w => (ILocation)new AGSLocation(w.X + offset.X, w.Y + offset.Y, w.Z));
			}
			return new List<ILocation> ();
		}

		private bool isWalkable(ILocation location)
		{
            foreach (var area in getWalkableAreas()) 
			{
                if (area.IsInArea(location.XY)) return true;
			}
			return false;
		}

		private bool[][] getWalkableMask(out Point offset)
		{
            var walkables = getWalkableAreas().ToList();
            var minX = (int)walkables.Min(a => a.Mask.MinX);
            var maxX = (int)walkables.Max(a => a.Mask.MaxX);
            var minY = (int)walkables.Min(a => a.Mask.MinY);
            var maxY = (int)walkables.Max(a => a.Mask.MaxY);
            int width = maxX - minX;
            int height = maxY - minY;
            offset = new Point(minX, minY);
			bool[][] mask = new bool[width][];
            for (int i = 0; i < mask.Length; i++) mask[i] = new bool[height];
			foreach (var area in walkables) 
			{
				area.Mask.ApplyToMask(mask, offset);
			}
			return mask;
		}

        private IEnumerable<IArea> getWalkableAreas()
        {
            var room = _obj.Room;
            if (room == null) return Array.Empty<IArea>();
            return room.Areas.Where(area => 
            {
                if (!area.Enabled) return false;
                var walkable = area.GetComponent<IWalkableArea>();
                if (walkable == null || !walkable.IsWalkable) return false;
                var restrictionArea = area.GetComponent<IAreaRestriction>();
                return (restrictionArea == null || !restrictionArea.IsRestricted(_obj.ID));
            });
        }

		private async Task<bool> walkStraightLine(ILocation destination, 
			CancellationTokenSource token, List<IObject> debugRenderers)
		{
			if (debugRenderers != null) 
			{
                GLLineRenderer line = new GLLineRenderer (_glUtils, _obj.X, _obj.Y, destination.X, destination.Y);
				IObject renderer = _objFactory.GetObject("Debug Line");
				renderer.CustomRenderer = line;
				await renderer.ChangeRoomAsync(_obj.Room);
				debugRenderers.Add (renderer);
			}

            if (_cutscene.IsSkipping)
            {
                _obj.X = destination.X;
                _obj.Y = destination.Y;
                return true;
            }

            if (!isDistanceVeryShort(destination))
			{
				var lastDirection = _faceDirection.Direction;
                var walkAnimation = _outfit.Outfit[AGSOutfit.Walk];
                bool alreadyWalking = _faceDirection.CurrentDirectionalAnimation == walkAnimation;
                _faceDirection.CurrentDirectionalAnimation = walkAnimation;
				await _faceDirection.FaceDirectionAsync(_obj.X, _obj.Y, destination.X, destination.Y);
				if (lastDirection != _faceDirection.Direction && alreadyWalking)
				{
					await Task.Delay(200);
				}
			}

			float xSteps = Math.Abs (destination.X - _obj.X);
			float ySteps = Math.Abs (destination.Y - _obj.Y);

			float numSteps = Math.Max (xSteps, ySteps);
			bool isBaseStepX = xSteps >= ySteps;

            float xStep = xSteps / numSteps;
			if (_obj.X > destination.X) xStep = -xStep;

			float yStep = ySteps / numSteps;
			if (_obj.Y > destination.Y) yStep = -yStep;

			WalkLineInstruction instruction = new WalkLineInstruction(token, numSteps, xStep, yStep, 
                                                                      isBaseStepX, destination);
            _currentWalkLine = instruction;
            Task timeout = Task.Delay(WalkLineTimeoutInMilliseconds);
			Task completedTask = await Task.WhenAny(instruction.OnCompletion.Task, timeout);

            if (completedTask == timeout)
            {
                instruction.CancelToken.Cancel();
                return false;
            }

            if (instruction.CancelToken.IsCancellationRequested || !isWalkable(_obj.Location))
                return false;
			
			_obj.X = destination.X;
			_obj.Y = destination.Y;
			return true;
		}

		private bool isDistanceVeryShort(ILocation destination)
		{
			var deltaX = destination.X - _obj.X;
			var deltaY = destination.Y - _obj.Y;
			return (deltaX * deltaX) + (deltaY * deltaY) < 10;
		}

        private PointF adjustWalkSpeed(PointF walkSpeed)
		{
            walkSpeed = adjustWalkSpeedBasedOnArea(walkSpeed);
			return walkSpeed;
		}

        private PointF adjustWalkSpeedBasedOnArea(PointF walkSpeed)
        {
			if (_obj == null || _obj.Room == null || _obj.Room.Areas == null ||
				_obj.IgnoreScalingArea || !AdjustWalkSpeedToScaleArea) return walkSpeed;
            
            foreach (var area in _obj.Room.Areas)
            {
				if (!area.Enabled || !area.IsInArea(_obj.Location.XY)) continue;
                var scalingArea = area.GetComponent<IScalingArea>();
                if (scalingArea == null || (!scalingArea.ScaleObjectsX && !scalingArea.ScaleObjectsY)) continue;
                float scale = scalingArea.GetScaling(scalingArea.Axis == ScalingAxis.X ? _obj.X : _obj.Y);
                if (scale != 1f)
                {
                    walkSpeed = new PointF(walkSpeed.X * (scalingArea.ScaleObjectsX ? scale : 1f), 
                                           walkSpeed.Y * (scalingArea.ScaleObjectsY ? scale : 1f));
                    if (walkSpeed.X == 0f || walkSpeed.Y == 0f)
                    {
                        walkSpeed = new PointF(walkSpeed.X == 0f ? 1f : walkSpeed.X,
                                               walkSpeed.Y == 0f ? 1f : walkSpeed.Y);
                    }
                }
                break;
            }
            return walkSpeed;
        }

        private class WalkLineInstruction
        {
			public WalkLineInstruction(CancellationTokenSource token, float numSteps, float xStep, float yStep, 
                                       bool isBaseStepX, ILocation destination)
            {
                CancelToken = token;
                NumSteps = numSteps;
                XStep = xStep;
                YStep = yStep;
				IsBaseStepX = isBaseStepX;
                OnCompletion = new TaskCompletionSource<object>();
                Destination = destination;
            }

            public CancellationTokenSource CancelToken { get; private set; }
            public TaskCompletionSource<object> OnCompletion { get; private set; }
			public float NumSteps { get; set; }
            public float XStep { get; private set; }
            public float YStep { get; private set; }
			public bool IsBaseStepX { get; private set; }
            public ILocation Destination { get; private set; }
        }
    }
}

