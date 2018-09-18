﻿using System.Reflection;
using AGS.API;

namespace AGS.Editor
{
    public interface IInspectorTreeNode : ITreeStringNode
    {
        IProperty Property { get; }

        IInspectorPropertyEditor Editor { get; }
    }
}