﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AGS.API;
using Veldrid.Sdl2;

namespace AGS.Engine.Desktop
{
    public class AGSInput : IInput
    {
        private Sdl2Window _game;
        private float _mouseX, _mouseY;
        private readonly IShouldBlockInput _shouldBlockInput;
        private readonly IConcurrentHashSet<API.Key> _keysDown;
        private readonly IGameEvents _events;
        private readonly ICoordinates _coordinates;
        private readonly IAGSCursor _cursor;
        private readonly IAGSHitTest _hitTest;

        private bool _originalOSCursor;
        private readonly ConcurrentQueue<Func<Task>> _actions;
        private int _inUpdate; //For preventing re-entrancy

        public AGSInput(IGameEvents events, IShouldBlockInput shouldBlockInput, IAGSCursor cursor, IAGSHitTest hitTest,
                        IEvent<API.MouseButtonEventArgs> mouseDown, 
                        IEvent<API.MouseButtonEventArgs> mouseUp, IEvent<MousePositionEventArgs> mouseMove,
                        IEvent<KeyboardEventArgs> keyDown, IEvent<KeyboardEventArgs> keyUp, ICoordinates coordinates)
        {
            _cursor = cursor;
            _events = events;
            _hitTest = hitTest;
            _actions = new ConcurrentQueue<Func<Task>>();
            _coordinates = coordinates;
            _shouldBlockInput = shouldBlockInput;
            _keysDown = new AGSConcurrentHashSet<API.Key>();

            MouseDown = mouseDown;
            MouseUp = mouseUp;
            MouseMove = mouseMove;
            KeyDown = keyDown;
            KeyUp = keyUp;

            if (VeldridGameWindow.GameWindow != null) init(VeldridGameWindow.GameWindow);
            else VeldridGameWindow.OnInit = () => init(VeldridGameWindow.GameWindow);
        }

        private void init(Sdl2Window game)
        {
            if (_game != null) return;
            _game = game;
            this._originalOSCursor = game.CursorVisible;

            _cursor.PropertyChanged += (sender, e) =>
            {
                if (_cursor.Cursor != null) _game.CursorVisible = false;
            };
            game.MouseDown += (e) =>
            {
                if (isInputBlocked()) return;
                var button = convert(e.MouseButton);
                _actions.Enqueue(() =>
                {
                    if (button == MouseButton.Left) LeftMouseButtonDown = true;
                    else if (button == MouseButton.Right) RightMouseButtonDown = true;
                    return MouseDown.InvokeAsync(new AGS.API.MouseButtonEventArgs(_hitTest.ObjectAtMousePosition, button, MousePosition));
                });
            };
            game.MouseUp += (e) =>
            {
                if (isInputBlocked()) return;
                var button = convert(e.MouseButton);
                _actions.Enqueue(() => 
                {
                    if (button == MouseButton.Left) LeftMouseButtonDown = false;
                    else if (button == MouseButton.Right) RightMouseButtonDown = false;
                    return MouseUp.InvokeAsync(new AGS.API.MouseButtonEventArgs(_hitTest.ObjectAtMousePosition, button, MousePosition));
                });
            };
            game.MouseMove += (e) =>
            {
                _mouseX = e.MousePosition.X;
                _mouseY = e.MousePosition.Y;
                _actions.Enqueue(() => MouseMove.InvokeAsync(new MousePositionEventArgs(MousePosition)));
            };
            game.KeyDown += (e) =>
            {
                API.Key key = convert(e.Key);
                _keysDown.Add(key);
                if (isInputBlocked()) return;
                _actions.Enqueue(() => KeyDown.InvokeAsync(new KeyboardEventArgs(key)));
            };
            game.KeyUp += (e) =>
            {
                API.Key key = convert(e.Key);
                _keysDown.Remove(key);
                if (isInputBlocked()) return;
                _actions.Enqueue(() => KeyUp.InvokeAsync(new KeyboardEventArgs(key)));
            };

            _events.OnRepeatedlyExecuteAlways.Subscribe(onRepeatedlyExecute);
        }

        #region IInputEvents implementation

        public IEvent<API.MouseButtonEventArgs> MouseDown { get; private set; }

        public IEvent<API.MouseButtonEventArgs> MouseUp { get; private set; }

        public IEvent<MousePositionEventArgs> MouseMove { get; private set; }

        public IEvent<KeyboardEventArgs> KeyDown { get; private set; }

        public IEvent<KeyboardEventArgs> KeyUp { get; private set; }

        #endregion

        public bool IsKeyDown(API.Key key) => _keysDown.Contains(key);

        public MousePosition MousePosition => new MousePosition(_mouseX, _mouseY, _coordinates);

        public bool LeftMouseButtonDown { get; private set; }
        public bool RightMouseButtonDown { get; private set; }
        public bool IsTouchDrag => false;  //todo: support touch screens on desktops

        public bool ShowHardwareCursor
        {
            get => _game.CursorVisible;
            set => _game.CursorVisible = value;
        }

        private bool isInputBlocked() => _shouldBlockInput.ShouldBlockInput();

        private AGS.API.MouseButton convert(Veldrid.MouseButton button)
        {
            switch (button)
            {
                case Veldrid.MouseButton.Left:
                    return AGS.API.MouseButton.Left;
                case Veldrid.MouseButton.Right:
                    return AGS.API.MouseButton.Right;
                case Veldrid.MouseButton.Middle:
                    return AGS.API.MouseButton.Middle;
                default:
                    throw new NotSupportedException();
            }
        }

        private void onRepeatedlyExecute()
        {
            _hitTest.Refresh(MousePosition);
            if (Interlocked.CompareExchange(ref _inUpdate, 1, 0) != 0) return;
            try
            {
                while (_actions.TryDequeue(out var action))
                {
                    action();
                }
            }
            finally
            {
                _inUpdate = 0;
            }
        }

        private AGS.API.Key convert(Veldrid.Key key) => (AGS.API.Key)(int)key;
    }
}
