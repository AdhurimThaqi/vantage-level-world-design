using UnityEngine;
using UnityEditor;

using System.IO;
using CoverShooter.AI;

namespace CoverShooter
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "brain")]
    public class BrainImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
        {
            Brain brain = null;

            var window = EditorWindow.GetWindow<BrainEditor>(false, "Brain", false);

            if (window != null && window.Brain != null)
            {
                var path = AssetDatabase.GetAssetPath(window.Brain);

                if (path == ctx.assetPath)
                    brain = window.Brain;
            }

            if (brain == null)
                brain = new Brain();

            JsonUtility.FromJsonOverwrite(File.ReadAllText(ctx.assetPath), brain);

            ctx.AddObjectToAsset("brain", brain);
            ctx.SetMainObject(brain);
        }
    }
}
