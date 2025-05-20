using HarmonyLib;
using MonkeyLoader.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.ModTemplate
{
    [HarmonyPatch("SomeType", "SomeMethod")]
    [HarmonyPatchCategory(nameof(BasicPatcher))]
    internal sealed class BasicPatcher : Monkey<BasicPatcher>
    {
        // The options for these should be provided by your game's game pack.
        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        private static void Postfix()
        {
            Logger.Info(() => "Postfix for SomeType.SomeMethod()!");
        }
    }
}