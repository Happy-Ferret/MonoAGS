﻿using AGS.API;
using OpenTK;

namespace AGS.Engine
{
	public interface IGLMatrixBuilder
	{
		IGLMatrices Build(IHasModelMatrix obj, IHasModelMatrix sprite, IObject parent, Matrix4 viewport, PointF areaScaling);
	}
}
