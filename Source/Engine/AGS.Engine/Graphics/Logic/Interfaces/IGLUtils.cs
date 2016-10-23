﻿using AGS.API;
using OpenTK;

namespace AGS.Engine
{
    public interface IGLUtils
    {
        void AdjustResolution(int width, int height);
        void RefreshViewport(IGameSettings settings, GameWindow gameWindow);
        void GenBuffer();
        void DrawQuad(int texture, Vector3 bottomLeft, Vector3 bottomRight,
                      Vector3 topLeft, Vector3 topRight, float r, float g, float b, float a);
        void DrawQuad(int texture, Vector3 bottomLeft, Vector3 bottomRight,
            Vector3 topLeft, Vector3 topRight, IGLColor bottomLeftColor, IGLColor bottomRightColor,
                      IGLColor topLeftColor, IGLColor topRightColor);
        void DrawQuad(int texture, Vector3 bottomLeft, Vector3 bottomRight,
                      Vector3 topLeft, Vector3 topRight, IGLColor color, FourCorners<Vector2> texturePos);
        void DrawTriangleFan(int texture, GLVertex[] vertices);
        void DrawCross(float x, float y, float width, float height,
                       float r, float g, float b, float a);
        void DrawLine(float x1, float y1, float x2, float y2,
                      float width, float r, float g, float b, float a);
    }
}
