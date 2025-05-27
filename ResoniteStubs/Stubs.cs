namespace ResoniteModLoader
{
    public abstract class ResoniteModBase
    {
        internal bool FinishedLoading { get; set; }
    }

    public abstract class ResoniteMod : ResoniteModBase
    {
        public abstract string Name { get; }
        public abstract string Author { get; }
        public abstract string Version { get; }
        public abstract string Link { get; }
        public virtual void OnEngineInit() { }
        public virtual ModConfiguration? GetConfiguration() => new();

        public static bool IsDebugEnabled() => true;
        public static void Debug(string message) { }
        public static void DebugFunc(System.Func<string> func) { }
        public static void Warn(string message) { }
        public static void Warn(System.Exception ex) { }
        public static void Msg(string message) { }
    }

    public class ModConfiguration
    {
        public T? GetValue<T>(ModConfigurationKey<T> key) => default;
    }

    public class ModConfigurationKey<T>
    {
        public ModConfigurationKey(string name, System.Func<T>? computeDefault = null) { }
        public event System.Action<T?>? OnChanged;
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class AutoRegisterConfigKeyAttribute : System.Attribute { }
}

namespace ResoniteHotReloadLib
{
    using ResoniteModLoader;
    public static class HotReloader
    {
        public static void RegisterForHotReload(ResoniteMod mod) { }
    }
}

namespace Elements.Core
{
    public struct float3
    {
        public float x;
        public float y;
        public float z;
        public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct color { public float r; public float g; public float b; public float a; }

    public struct colorX
    {
        public color Color;
        public string profile;
        public static explicit operator colorX(color c) => new colorX { Color = c, profile = string.Empty };
    }
}

namespace FrooxEngine
{
    using Elements.Core;

    public interface ISyncMember { }
    public class DataTreeNode { }
    public class LoadControl { }
    public class SaveControl { }

    public struct RefID
    {
        private int _id;
        public RefID(int id) { _id = id; }
        public override string ToString() => $"ID{_id}";
        public static bool TryParse(string? s, out RefID id)
        {
            if (s != null && s.StartsWith("ID") && int.TryParse(s.Substring(2), out var v))
            {
                id = new RefID(v);
                return true;
            }
            id = default;
            return false;
        }
    }

    public interface IWorldElement
    {
        RefID ReferenceID { get; }
        string Name { get; }
        IWorldElement? Parent { get; }
    }

    public class ReferenceController
    {
        public object? GetObjectOrNull(RefID id) => null;
    }

    public class Slot : IWorldElement
    {
        public RefID ReferenceID { get; set; }
        public string Name { get; set; } = string.Empty;
        public IWorldElement? Parent { get; set; }
        public World World { get; set; } = new World();
        public float3 LocalPosition { get; set; }
        public float3 GlobalScale { get; set; }
        public string Tag { get; set; } = string.Empty;
        public Slot AddSlot(string name) => new Slot { Name = name, Parent = this, World = World };
        public System.Collections.Generic.IEnumerable<Slot> GetChildrenWithTag(string tag) => System.Linq.Enumerable.Empty<Slot>();
        public void RunSynchronously(System.Action action) => action();
        public void UnpackNodes() { }
        public object AttachComponent(System.Type type) => new object();
        public void Destroy() { }
        public void PositionInFrontOfUser() { }
    }

    public class TypeManager
    {
        public string EncodeType(System.Type type) => type.FullName ?? string.Empty;
        public System.Type? DecodeType(string name) => System.Type.GetType(name);
    }

    public class World
    {
        public Slot RootSlot { get; } = new Slot();
        public Slot LocalUserSpace { get; } = new Slot();
        public TypeManager Types { get; } = new TypeManager();
        public ReferenceController ReferenceController { get; } = new ReferenceController();
    }

    public class WorldManager
    {
        public World FocusedWorld { get; } = new World();
    }

    public class Engine
    {
        public static Engine Current { get; } = new Engine();
        public WorldManager WorldManager { get; } = new WorldManager();
    }

    public static class WorkerInitializer
    {
        public static ComponentLibrary ComponentLibrary { get; } = new ComponentLibrary();
    }

    public class ComponentLibrary
    {
        public CategoryNode<System.Type> GetSubcategory(string path) => new CategoryNode<System.Type>();
    }

    public class CategoryNode<T>
    {
        public string Name { get; set; } = string.Empty;
        public System.Collections.Generic.List<CategoryNode<T>> Subcategories { get; } = new();
        public System.Collections.Generic.List<T> Elements { get; } = new();
        public int ElementCount => Elements.Count;
    }
}

namespace FrooxEngine.ProtoFlux
{
    using FrooxEngine;

    public interface IInput
    {
        object? BoxedValue { get; set; }
        System.Type InputType();
    }

    public class ProtoFluxNode : IWorldElement
    {
        public RefID ReferenceID { get; set; }
        public string Name { get; set; } = string.Empty;
        public IWorldElement? Parent { get; set; }
        public Slot Slot { get; } = new Slot();

        public int NodeInputCount => 0;
        public int NodeInputListCount => 0;
        public int NodeOutputCount => 0;
        public int NodeOutputListCount => 0;
        public int NodeImpulseCount => 0;
        public int NodeImpulseListCount => 0;
        public int NodeOperationCount => 0;
        public int NodeOperationListCount => 0;
        public int NodeReferenceCount => 0;
        public int NodeGlobalRefCount => 0;
        public int NodeGlobalRefListCount => 0;

        public object GetInput(int index) => new object();
        public object GetOutput(int index) => new object();
        public object GetImpulse(int index) => new object();
        public object GetOperation(int index) => new object();
        public object GetReference(int index) => new object();
        public object GetInputList(int index) => new object();
        public object GetOutputList(int index) => new object();
        public object GetImpulseList(int index) => new object();
        public object GetOperationList(int index) => new object();
        public object GetGlobalRef(int index) => new object();
        public object GetGlobalRefList(int index) => new object();

        public bool TryConnectInput(object src, object dst, bool allowExplicitCast, bool undoable) => true;
        public bool TryConnectImpulse(object src, object dst, bool undoable) => true;
        public bool TryConnectReference(object src, ProtoFluxNode target, bool undoable) => true;
    }
}
