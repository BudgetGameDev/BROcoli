using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeGuardEntryCoverageTests
    {
        private void PopulateFields(Type type, object target, int variant)
        {
            if (target == null || target is UnityEngine.Object item && item == null)
                return;
            for (Type current = type; current != null; current = current.BaseType)
            {
                foreach (
                    FieldInfo field in current.GetFields(
                        BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.DeclaredOnly
                    )
                )
                {
                    if (field.IsLiteral || field.FieldType.IsPointer)
                        continue;
                    try
                    {
                        object existing = field.GetValue(target);
                        PopulateCollection(existing, field.FieldType, variant);
                        if (field.IsInitOnly)
                            continue;
                        if (existing == null || field.FieldType.IsValueType)
                            field.SetValue(target, Value(field.FieldType, variant));
                    }
                    catch
                    {
                        // Native-backed and invariant fields may reject synthetic state.
                    }
                }
            }
        }

        private object[] Arguments(MethodInfo method, int variant)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (parameterType.IsByRef)
                    parameterType = parameterType.GetElementType();
                if (parameterType.IsArray)
                {
                    Type elementType = parameterType.GetElementType();
                    Array array = Array.CreateInstance(elementType, variant == 0 ? 0 : 1);
                    if (array.Length != 0)
                        array.SetValue(Value(elementType, variant), 0);
                    arguments[index] = array;
                }
                else
                    arguments[index] = Value(parameterType, variant);
            }
            return arguments;
        }

        private object Value(Type type, int variant)
        {
            if (variant == 0)
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            if (type == typeof(string))
                return variant > 0 ? "coverage" : string.Empty;
            if (type == typeof(bool))
                return variant > 0;
            if (type.IsEnum)
            {
                if (variant < 0)
                    return Enum.ToObject(type, int.MaxValue);
                Array values = Enum.GetValues(type);
                return values.GetValue(Math.Abs(variant) % values.Length);
            }
            if (type == typeof(int))
                return variant;
            if (type == typeof(float))
                return (float)variant;
            if (type == typeof(double))
                return (double)variant;
            if (type == typeof(long))
                return (long)variant;
            if (type == typeof(Vector2))
                return Vector2.one * variant;
            if (type == typeof(Vector2Int))
                return Vector2Int.one * variant;
            if (type == typeof(Vector3))
                return Vector3.one * variant;
            if (type == typeof(Vector3Int))
                return Vector3Int.one * variant;
            if (type == typeof(Quaternion))
                return Quaternion.Euler(Vector3.one * variant);
            if (type == typeof(Color))
                return variant > 0 ? Color.white : Color.black;
            if (type == typeof(Rect))
                return new Rect(variant, variant, 1f, 1f);
            if (type == typeof(System.Random))
                return new System.Random(1234);
            if (type == typeof(AnimationCurve))
                return AnimationCurve.Linear(0f, 0f, 1f, 1f);
            if (type == typeof(PointerEventData))
                return new PointerEventData(EventSystem.current);
            if (type == typeof(VertexHelper))
                return new VertexHelper();
            if (type == typeof(GameObject))
                return CreateGameObject("Coverage argument");
            if (type == typeof(Transform))
                return CreateGameObject("Coverage transform").transform;
            if (type == typeof(Material))
                return Track(new Material(Shader.Find("Sprites/Default")));
            if (type == typeof(Texture2D) || type == typeof(Texture))
                return Track(new Texture2D(2, 2));
            if (type == typeof(Sprite))
            {
                Texture2D texture = Track(new Texture2D(2, 2));
                return Track(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f));
            }
            if (type == typeof(Mesh))
                return Track(new Mesh());
            if (type == typeof(AudioClip))
                return Track(AudioClip.Create("Coverage", 16, 1, 8000, false));
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                Array array = Array.CreateInstance(elementType, 1);
                array.SetValue(Value(elementType, variant), 0);
                return array;
            }
            if (typeof(Component).IsAssignableFrom(type) || type.IsInterface)
            {
                Type componentType = ConcreteComponentType(type);
                if (componentType != null)
                    return CreateInitializedComponent(componentType);
            }
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                Type concrete =
                    definition == typeof(List<>) || definition == typeof(HashSet<>) ? type
                    : definition == typeof(Dictionary<,>) ? type
                    : definition == typeof(IEnumerable<>)
                    || definition == typeof(IReadOnlyList<>)
                    || definition == typeof(IList<>)
                        ? typeof(List<>).MakeGenericType(type.GetGenericArguments())
                    : null;
                if (concrete != null)
                {
                    object collection = Activator.CreateInstance(concrete);
                    PopulateCollection(collection, concrete, variant);
                    return collection;
                }
            }
            if (!type.IsAbstract && !type.IsInterface)
            {
                try
                {
                    return Activator.CreateInstance(type, nonPublic: true);
                }
                catch
                {
                    // A non-null sample is unavailable for this reference type.
                }
            }
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private void PopulateCollection(object collection, Type type, int variant)
        {
            if (collection == null || !type.IsGenericType)
                return;
            Type[] arguments = type.GetGenericArguments();
            if (arguments.Length == 1)
            {
                PropertyInfo count = type.GetProperty("Count");
                if (count != null && (int)count.GetValue(collection) != 0)
                    return;
                MethodInfo add = type.GetMethod("Add", new[] { arguments[0] });
                add?.Invoke(collection, new[] { Value(arguments[0], variant) });
            }
            else if (arguments.Length == 2)
            {
                PropertyInfo count = type.GetProperty("Count");
                if (count != null && (int)count.GetValue(collection) != 0)
                    return;
                MethodInfo add = type.GetMethod("Add", arguments);
                add?.Invoke(
                    collection,
                    new[] { Value(arguments[0], variant), Value(arguments[1], variant) }
                );
            }
        }

        private Type ConcreteComponentType(Type requested)
        {
            Type[] builtIns =
            {
                typeof(BoxCollider),
                typeof(BoxCollider2D),
                typeof(Rigidbody),
                typeof(Rigidbody2D),
                typeof(MeshRenderer),
                typeof(ParticleSystem),
                typeof(AudioSource),
            };
            Type builtIn = builtIns.FirstOrDefault(requested.IsAssignableFrom);
            if (builtIn != null)
                return builtIn;
            return typeof(PlayerStats)
                .Assembly.GetTypes()
                .Where(type =>
                    !type.IsAbstract
                    && typeof(Component).IsAssignableFrom(type)
                    && requested.IsAssignableFrom(type)
                )
                .OrderBy(type => type.FullName)
                .FirstOrDefault();
        }

        private Component CreateInitializedComponent(Type componentType)
        {
            GameObject item = CreateGameObject($"Coverage {componentType.Name}");
            if (typeof(EnemyBase).IsAssignableFrom(componentType))
                item.AddComponent<BoxCollider>();
            Component component = item.AddComponent(componentType);
            if (component is PlayerStats stats)
                stats.ResetStats();
            MethodInfo awake = componentType.GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            try
            {
                awake?.Invoke(component, null);
            }
            catch
            {
                // Some components need scene references beyond their own object.
            }
            return component;
        }

        private GameObject CreateGameObject(string name)
        {
            var item = new GameObject(name);
            item.SetActive(false);
            createdObjects.Add(item);
            return item;
        }

        private T Track<T>(T item)
            where T : UnityEngine.Object
        {
            createdObjects.Add(item);
            return item;
        }
    }
}
