using MonkeyLoader.Patching;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ModTemplate
{
    internal sealed class BasicPrePatcher : ConfiguredEarlyMonkey<BasicPrePatcher, MyConfig>
    {
        public static void HelloMethod()
            => Logger.Info(() => $"Hello {ConfigSection.TargetName} from pre-patched-in SomeNameSpace.SomeType static constructor!");

        // The options for these should be provided by your game's game pack.
        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        protected override IEnumerable<PrePatchTarget> GetPrePatchTargets()
        {
            yield return new PrePatchTarget(new AssemblyName("Assembly-CSharp"), "SomeNameSpace.SomeType");
        }

        protected override bool Patch(PatchJob patchJob)
        {
            var engine = patchJob.Types.First();
            var engineCCtor = engine.GetStaticConstructor();

            var processor = engineCCtor.Body.GetILProcessor(); // using MonoMod.Utils; is important for this to work v
            processor.InsertBefore(engineCCtor.Body.Instructions.First(), processor.Create(OpCodes.Call, typeof(BasicPrePatcher).GetMethod(nameof(HelloMethod))));

            patchJob.Changes = true;
            return true;
        }
    }
}