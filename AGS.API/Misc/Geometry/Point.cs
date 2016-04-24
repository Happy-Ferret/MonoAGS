﻿using System;

namespace AGS.API
{
	public struct Point
	{
		private readonly int _x, _y;

		public Point(int x, int y)
		{
			_x = x;
			_y = y;
		}

		public int X { get { return _x; } }
		public int Y { get { return _y; } }
	}
}

