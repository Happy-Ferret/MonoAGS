﻿extern alias IOS;

using IOS::System;
using IOS::CoreAnimation;
using IOS::Foundation;
using IOS::ObjCRuntime;
using IOS::OpenGLES;
using IOS::UIKit;
using OpenTK.Platform.iPhoneOS;
using System;
using OpenTK.Graphics.ES20;
using System.Diagnostics;
using Autofac;

namespace AGS.Engine.IOS
{
    public class IOSGameView : iPhoneOSGameView, IUIKeyInput
    {
        int _frameInterval;
        CADisplayLink _displayLink;
        private FrameEventArgs _updateFrameArgs, _renderFrameArgs;

        public IOSGameView(NSCoder coder) : base(coder)
        {
            Debug.WriteLine("IOS Game View constructor");
            Resolver.Override(resolver => resolver.Builder.RegisterInstance(this));
            LayerRetainsBacking = true;
            LayerColorFormat = EAGLColorFormat.RGBA8;

            // retina support
            ContentScaleFactor = UIScreen.MainScreen.Scale;

            _updateFrameArgs = new FrameEventArgs();
            _renderFrameArgs = new FrameEventArgs();
            IOSGameWindow.Instance.View = this;
        }

        protected override void ConfigureLayer(CAEAGLLayer eaglLayer)
        {
            eaglLayer.Opaque = true;
        }

        protected override void CreateFrameBuffer()
        {
            Debug.WriteLine("IOS Game View create frame buffer");
            OpenTK.Toolkit.Init();
            nfloat screenScale = UIScreen.MainScreen.Scale;
            CAEAGLLayer eaglLayer = (CAEAGLLayer)Layer;
            //CGSize size = new CGSize(
            //    (int)Math.Round(screenScale * eaglLayer.Bounds.Size.Width),
            //    (int)Math.Round(screenScale * eaglLayer.Bounds.Size.Height));

            try
            {
                ContextRenderingApi = EAGLRenderingAPI.OpenGLES2;

                //OpenTK.Graphics.OpenGL.LoadAll();
                //OpenTK.Graphics.ES20.GL.ClearColor(1f, 1f, 1f, 1f);
                base.CreateFrameBuffer();

                Console.WriteLine("using ES 2.0");
                Console.WriteLine("version: {0} glsl version: {1}", GL.GetString(StringName.Version), GL.GetString(StringName.ShadingLanguageVersion));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                throw new Exception("Looks like OpenGL ES 2.0 not available", e);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Debug.WriteLine("IOS Game View create OnLoad");
            base.OnLoad(e);
        }

        public override bool CanBecomeFirstResponder
        {
            get
            {
                return true;
            }
        }

        public bool IsAnimating { get; private set; }

        // How many display frames must pass between each time the display link fires.
        public int FrameInterval
        {
            get
            {
                return _frameInterval;
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentException();
                _frameInterval = value;
                if (IsAnimating)
                {
                    StopAnimating();
                    StartAnimating();
                }
            }
        }

        public bool HasText { get; set; }

        public UITextAutocapitalizationType AutocapitalizationType { get; set; }

        public UITextAutocorrectionType AutocorrectionType { get; set; }

        public UIKeyboardType KeyboardType { get; set; }

        public UIKeyboardAppearance KeyboardAppearance { get; set; }

        public UIReturnKeyType ReturnKeyType { get; set; }

        public bool EnablesReturnKeyAutomatically { get; set; }

        public bool SecureTextEntry { get; set; }

        public UITextSpellCheckingType SpellCheckingType { get; set; }

        public void StartAnimating()
        {
            if (IsAnimating)
                return;

            base.UpdateFrame += onUpdateFrame;
            base.RenderFrame += onRenderFrame;
            IOSGameWindow.Instance.OnLoad(new EventArgs());

            _displayLink = UIScreen.MainScreen.CreateDisplayLink(this, new Selector("drawFrame"));
            _displayLink.FrameInterval = _frameInterval;
            _displayLink.AddToRunLoop(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);

            IsAnimating = true;
        }

        public void StopAnimating()
        {
            if (!IsAnimating)
                return;

            _displayLink.Invalidate();
            _displayLink = null;
            DestroyFrameBuffer();
            IsAnimating = false;
        }

        public event EventHandler<string> OnInsertText;
        public event EventHandler<EventArgs> OnDeleteBackward;

        public void InsertText(string text)
        {
            HasText = true;
            OnInsertText?.Invoke(this, text);
        }

        public void DeleteBackward()
        {
            OnDeleteBackward?.Invoke(this, null);
        }

        private void onUpdateFrame(object sender, OpenTK.FrameEventArgs args)
        {
            _updateFrameArgs.Time = args.Time;
            IOSGameWindow.Instance.OnUpdateFrame(_updateFrameArgs);
        }

        private void onRenderFrame(object sender, OpenTK.FrameEventArgs args)
        {
            _renderFrameArgs.Time = args.Time;
            IOSGameWindow.Instance.OnRenderFrame(_renderFrameArgs);
        }
    }
}
