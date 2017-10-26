using System.Threading.Tasks;
using AGS.API;
using Autofac;

namespace AGS.Engine
{
	public partial class AGSCharacter
	{
        partial void beforeInitComponents(Resolver resolver, IOutfit outfit)
        {
            TypedParameter objParameter = new TypedParameter(typeof(IObject), this);
            if (outfit != null) _faceDirectionBehavior.CurrentDirectionalAnimation = outfit[AGSOutfit.Idle];
            ISayLocationProvider location = resolver.Container.Resolve<ISayLocationProvider>(objParameter);
            TypedParameter locationParameter = new TypedParameter(typeof(ISayLocationProvider), location);
            _sayBehavior = resolver.Container.Resolve<ISayBehavior>(locationParameter);

            //todo: refactor ISayLocationProvider into a component, then bind to it from say behavior, then add walk & say to the template and remove from here
            _walkBehavior = AddComponent<IWalkBehavior>();
            AddComponent(_sayBehavior);
        }

        partial void afterInitComponents(Resolver resolver, IOutfit outfit)
		{
			RenderLayer = AGSLayers.Foreground;
			IgnoreScalingArea = false;

			_hasOutfit.Outfit = outfit;
		}

		public ILocation Location
		{
			get { return _translateComponent.Location; } 
			set
			{
				StopWalking();
                _translateComponent.Location = value; 
			}
		}

		public Task ChangeRoomAsync(IRoom room, float? x = null, float? y = null)
		{
			StopWalking();
			return _hasRoom.ChangeRoomAsync(room, x, y);
		}
	}
}

