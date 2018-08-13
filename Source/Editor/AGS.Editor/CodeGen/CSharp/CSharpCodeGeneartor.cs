﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using AGS.API;

namespace AGS.Editor
{
	public class CSharpCodeGeneartor : ICodeGenerator
    {
        private StateModel _stateModel;
        private Lazy<List<(Type returnType, Func<string> getPrefix, ParameterInfo[] pars)>> _factories;

        public CSharpCodeGeneartor(StateModel stateModel)
        {
            _stateModel = stateModel;
            _factories = new Lazy<List<(Type returnType, Func<string> getPrefix, ParameterInfo[] pars)>>(getFactories);
        }

        public void GenerateCode(string namespaceName, EntityModel model, StringBuilder code)
        {
            if (!model.IsDirty || model.Parent != null) return;
            code.AppendLine(generateClass(model, namespaceName));
        }

        private string generateDisplayName(EntityModel model)
        {
            if (model.DisplayName == null) return "";
            return $"{model.ScriptName}.{nameof(IEntity.DisplayName)} = {'"'}{model.DisplayName}{'"'};";
        }

        private string generateModels(EntityModel model, Func<EntityModel, string> generateLine, int numIndents = 12)
        {
            StringBuilder code = new StringBuilder();
            List<EntityModel> models = new List<EntityModel>();
            getAllModels(model, models);
            foreach (var entity in models)
            {
                string line = generateLine(entity);
                if (!string.IsNullOrEmpty(line))
                {
                    indent(code, numIndents).AppendLine(line);
                }
            }
            return code.ToString();
        }

        private void getAllModels(EntityModel model, List<EntityModel> models)
        {
            if (model == null) return;
            models.Add(model);
            foreach (var child in model.Children)
            {
                getAllModels(_stateModel.Entities[child], models);
            }
        }

        private string generateEntityMember(EntityModel model)
        {
            if (model.Initializer == null && !needEntity(model)) return "";
            return $"private {getType(model)} {model.ScriptName};";
        }

        private string generateEntityFactory(EntityModel model)
        {
            if (model.Initializer == null)
            {
                if (!needEntity(model)) return "";
                return $"{model.ScriptName} = ({getType(model)}){getRoot(model).ScriptName}.TreeNode.FindDescendant({'"'}{model.ID}{'"'});";
            }
            return $"{model.ScriptName} = {generateMethod(model.Initializer)}";
        }

        private EntityModel getRoot(EntityModel model)
        {
            if (model.Parent == null) return model;
            return getRoot(_stateModel.Entities[model.Parent]);
        }

        private bool needEntity(EntityModel model) => model.Components.Any(c => c.Value.Properties.Count > 0);

        private string getType(EntityModel model)
        {
            return model.Initializer?.ReturnType.Name ?? model.EntityConcreteType.Name;
        }

        private string generateComponents(EntityModel model)
        {
            StringBuilder code = new StringBuilder();
            foreach (var pair in model.Components)
            {
                bool isBuiltIn = pair.Key.IsAssignableFrom(model.EntityConcreteType);
                string componentName = isBuiltIn ? model.ScriptName : getComponentName(pair.Key.Name);
                if (!isBuiltIn)
                {
                    indent(code);
                    bool needVar = pair.Value.Properties.Count > 0;
                    if (needVar) code.Append($"var {componentName} = ");
                    code.AppendLine($"{model.ScriptName}.AddComponent<{pair.Key.Name}>();");
                }
                generateSetProperty(pair.Value, componentName, code);
            }
            return code.ToString();
        }

        private string generateAddToState(EntityModel model)
        {
            //todo: instead of generating add for each entity individually, we can use AddRange and batch all entities in this tree

            if (model.Initializer == null && !needEntity(model)) return "";
            if (_stateModel.Guis.Contains(model.ID))
            {
                return $"_state.UI.Add({model.ScriptName});";
            }
            foreach (var room in _stateModel.Rooms)
            {
                if (room.BackgroundEntity == model.ID)
                {
                    return $"{generateRoomCode(room.ID)}.Background = {model.ScriptName};";
                }
                if (!room.Entities.Contains(model.ID)) continue;
                if (typeof(IArea).IsAssignableFrom(model.EntityConcreteType))
                {
                    return $"{generateRoomCode(room.ID)}.Areas.Add({model.ScriptName});";
                }
                return $"{generateRoomCode(room.ID)}.Objects.Add({model.ScriptName});";
            }
            throw new InvalidOperationException($"Didn't find entity {model.ID} in state, can't generate code.");
        }

        //todo: use a static class for all rooms and use that instead of searching the state
        private string generateRoomCode(string roomId) =>  $"_state.Rooms.First(r => r.ID == {'"'}{roomId}{'"'})";

        private void generateScriptNames(EntityModel model)
        {
            List<EntityModel> models = new List<EntityModel>();
            getAllModels(model, models);
            foreach (var entity in models)
            {
                entity.ScriptName = getClassMemberName(entity.ID);
            }
        }

        private string generateClass(EntityModel model, string namespaceName)
        {
            generateScriptNames(model);
            string className = getClassName(model.ID);
            return
$@"using AGS.API;
using AGS.Engine;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace {namespaceName}
{{
    public class {className}
    {{
        private readonly IGameState _state;
        private readonly IGameFactory _factory;
{generateModels(model, generateEntityMember, 8)}

        public {className}(IGameState state, IGameFactory factory)
        {{
            _state = state;
            _factory = factory;
        }}

        public void Load()
        {{
{generateModels(model, generateEntityFactory)}
{generateModels(model, generateDisplayName)}
{generateModels(model, generateComponents, 0)}
{generateModels(model, generateAddToState)}
        }}
    }}
}}";
        }

        private StringBuilder indent(StringBuilder code, int chars = 12)
        {
            for (int i = 0; i < chars; i++) code.Append(' ');
            return code;
        }

        private string generateMethod(MethodModel model)
        {
            var parameters = string.Join(", ", model.Parameters.Select(p => getValueString(p)));
            if (string.IsNullOrEmpty(model.InstanceName))
            {
                return $"{model.Name}({parameters});";
            }
            return $"{model.InstanceName}.{model.Name}({parameters});";
        }

        private void generateSetProperty(ComponentModel model, string name, StringBuilder code)
        {
            foreach (var pair in model.Properties)
            {
                indent(code).AppendLine($"{name}.{pair.Key} = {getValueString(pair.Value)};");
            }
        }

        private string getComponentName(string interfaceName)
        {
            if (interfaceName.StartsWith("I", false, CultureInfo.InvariantCulture)) interfaceName = interfaceName.Substring(1);
            interfaceName = $"{char.ToLower(interfaceName[0])}{interfaceName.Substring(1)}";
            return getRawName(interfaceName);
        }

        private string getRawName(string name)
        {
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (!char.IsLetter(c) && !char.IsDigit(c) && c != '_') continue;
                sb.Append(c);
            }
            if (sb.Length == 0) throw new ArgumentException($"{name} is not a legal name: it must have at least one letter or digit");
            return sb.ToString();
        }

        private string getClassMemberName(string name)
        {
            name = getRawName(name);
            return name[0] == '_' ? name : $"_{name}";
        }

        private string getClassName(string name)
        {
            name = getRawName(name);
            var suffix = name.Length > 1 ? name.Substring(1) : "";
            return $"{char.ToUpperInvariant(name[0])}{suffix}";
        }

        private string getValueString(object val)
        {
            switch (val)
            {
                case null:
                    return "null";
                case float f:
                    return $"{f.ToString()}f";
                case double d1:
                    return $"{d1.ToString()}d";
                case decimal d2:
                    return $"{d2.ToString()}m";
                case byte v1:
                case int v2:
                case uint v3:
                case long v4:
                case ulong v5:
                case sbyte v6:
                case short v7:
                case ushort v8:
                    return val.ToString();
                case string s:
                    return $"{'"'}{s}{'"'}";
                case char c:
                    return $"{"'"}{c}{"'"}";
                case bool b:
                    return b.ToString().ToLowerInvariant();
                case ValueTuple t:
                    return tupleToString(t.GetType().GetProperties().Select(p => p.GetValue(t)));
                default:
                    Type type = val.GetType();
                    return deconstructToString(val, type) ?? enumToString(val, type) ?? factoryToString(val, type) ?? ctorToString(val, type) ?? val.ToString();
            }
        }

        private string deconstructToString(object val, Type type)
        {
            var deconstructMethod = (type.GetMethod("Deconstruct")); //Special keyword which allows deconstructing to tuples
            if (deconstructMethod == null) return null;

            var parameters = new object[deconstructMethod.GetParameters().Length];
            deconstructMethod.Invoke(val, parameters);
            return tupleToString(parameters);
        }

        private List<(Type returnType, Func<string> getPrefix, ParameterInfo[] pars)> getFactories(Type type, string typeString)
        {
            var methods = type.GetMethods();
            Func<string> getFactoryPrefix(MethodInfo s) => () => $"{typeString}.{s.Name}";
            var factories = methods.Select(m => (m.ReturnType, getFactoryPrefix(m), m.GetParameters())).ToList();
            var props = type.GetProperties();
            foreach (var prop in props)
            {
                factories.AddRange(getFactories(prop.PropertyType, $"{typeString}.{prop.Name}"));
            }
            return factories;
        }

        private List<(Type returnType, Func<string> getPrefix, ParameterInfo[] pars)> getFactories()
        {
            return getFactories(typeof(IGameFactory), "_factory");
        }

        private string factoryToString(object val, Type type)
        {
            var factories = _factories.Value.Where(f => f.returnType.IsAssignableFrom(type)).Select(f => (f.getPrefix, f.pars)).ToList();
            return bestParamsMatchToString(val, type, factories);
        }

        private string ctorToString(object val, Type type)
        {
            var ctors = type.GetConstructors();
            string typeString = typeNameToString(type);
            Func<string> ctorPrefix = () => $"new {typeString}";
            var factories = ctors.Select(c => (ctorPrefix, c.GetParameters())).ToList();

            var staticFactories = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.ReturnType.IsAssignableFrom(type));
            Func<string> getStaticPrefix(MethodInfo s) => () => $"{typeString}.{s.Name}";
            factories.AddRange(staticFactories.Select(s => (getStaticPrefix(s), s.GetParameters())));

            return bestParamsMatchToString(val, type, factories);
        }

        private string bestParamsMatchToString(object val, Type type, List<(Func<string> getPrefix, ParameterInfo[] pars)> factories)
        {
            if (factories.Count == 0) return null;
            var props = type.GetProperties();
            int bestMatchNumParams = -1;
            List<object> bestMatchParams = null;
            Func<string> bestMatchFactory = null;
            foreach (var factory in factories)
            {
                var pars = factory.pars;
                List<object> currentMatchParams = new List<object>(pars.Length);
                int currentNumMatchParams = 0;
                foreach (var param in pars)
                {
                    var matchingProps = props.Where(p => p.PropertyType == param.ParameterType).ToList();
                    if (matchingProps.Count == 1)
                    {
                        currentMatchParams.Add(matchingProps[0].GetValue(val));
                        currentNumMatchParams++;
                        continue;
                    }
                    if (matchingProps.Count > 1)
                    {
                        var matchingProp = matchingProps.FirstOrDefault(p => p.Name.ToLower() == param.Name.ToLower());
                        if (matchingProp != null)
                        {
                            currentMatchParams.Add(matchingProp.GetValue(val));
                            currentNumMatchParams++;
                            continue;
                        }
                    }
                    currentMatchParams.Add(getDefault(param.ParameterType));
                }
                if (currentNumMatchParams > bestMatchNumParams)
                {
                    bestMatchNumParams = currentNumMatchParams;
                    bestMatchParams = currentMatchParams;
                    bestMatchFactory = factory.getPrefix;
                }
            }
            var values = bestMatchParams.Select(p => getValueString(p));
            return $"{bestMatchFactory()}({string.Join(", ", values)})";
        }

        private string typeNameToString(Type type)
        {
            if (!type.IsGenericType) return type.Name;
            string prefix = type.Name.Substring(0, type.Name.Length - 2);
            var args = type.GenericTypeArguments.Select(t => typeNameToString(t));
            return $"{prefix}<{string.Join(", ", args)}>";
        }

        private object getDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        private string enumToString(object val, Type type)
        {
            if (type.IsEnum)
            {
                string valueString = val.ToString();
                return $"{typeNameToString(type)}.{valueString}";
            }
            return null;
        }

        private string tupleToString(IEnumerable<object> values)
        {
            var strings = values.Select(v => getValueString(v));
            return $"({string.Join(", ", strings)})";
        }
    }
}