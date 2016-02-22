﻿using System;
using AGS.API;
using OpenTK.Graphics.OpenGL;
using System.Drawing;

namespace AGS.Engine
{
	public class GLImage : IImage
	{
		public GLImage ()
		{
			Width = 1f;
			Height = 1f;
			ID = "";
		}

		public GLImage(Bitmap bitmap, string id, int texture, ISpriteSheet spriteSheet, ILoadImageConfig loadConfig)
		{
			OriginalBitmap = bitmap;
			Width = bitmap.Width;
			Height = bitmap.Height;
			ID = id;
			Texture = texture;
			SpriteSheet = spriteSheet;
			LoadConfig = loadConfig;
		}

		public Bitmap OriginalBitmap { get; private set; }

		public float Width { get; private set; }
		public float Height { get; private set; }

		public string ID { get; private set; }
		public int Texture { get; private set; }

		public ISpriteSheet SpriteSheet { get; private set; }
		public ILoadImageConfig LoadConfig { get; private set; }

		public override string ToString()
		{
			return ID ?? base.ToString();
		}
	}
}

