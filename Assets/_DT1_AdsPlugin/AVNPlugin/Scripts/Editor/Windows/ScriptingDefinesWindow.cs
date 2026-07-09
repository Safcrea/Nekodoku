using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

public class ScriptingSymbolManager : OdinEditorWindow
{
    [MenuItem("AVN/Manage Scripting Symbols")]
    private static void OpenWindow() => GetWindow<ScriptingSymbolManager>().Show();

    [TitleGroup("Required Symbols")]
    [ListDrawerSettings(
        HideAddButton = true, 
        HideRemoveButton = true, 
        DraggableItems = false, 
        ShowFoldout = false,
        DefaultExpandedState = true
    )]
    [ShowInInspector]
    private List<SymbolState> requiredSymbols = new List<SymbolState>
    {
        new SymbolState("USE_AVNADS_PLUGIN"),
        new SymbolState("USE_ADMOB"),
        new SymbolState("USE_FIREBASE")
    };

    // ---- Ad Providers: only ONE may be active at a time ----
    [TitleGroup("Ad SDK Provider (select one)")]
    [InfoBox("Only one Ad SDK provider can be active at a time. Enabling one will automatically disable the other.")]
    [ListDrawerSettings(
        HideAddButton = true,
        HideRemoveButton = true,
        DraggableItems = false,
        ShowFoldout = false,
        DefaultExpandedState = true
    )]
    [ShowInInspector]
    private List<ExclusiveSymbolState> adProviderSymbols = new List<ExclusiveSymbolState>
    {
        new ExclusiveSymbolState("USE_LEVELPLAY", new[] { "USE_MAX" }),
        new ExclusiveSymbolState("USE_MAX",       new[] { "USE_LEVELPLAY" })
    };

    [TitleGroup("Optional Symbols")]
    [ListDrawerSettings(
        HideAddButton = true, 
        HideRemoveButton = true, 
        DraggableItems = false, 
        ShowFoldout = false, 
        DefaultExpandedState = true
    )]
    [ShowInInspector]
    private List<SymbolState> optionalSymbols = new List<SymbolState>
    {
        new SymbolState("USE_AVNADS_PLUGIN_DEBUG"),
        new SymbolState("USE_BYTEBREW"),
        new SymbolState("USE_IAP")
    };

    [OnInspectorGUI]
    private void RefreshStates()
    {
        NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        var activeSymbols = new HashSet<string>(currentDefines.Split(';'));

        foreach (var s in requiredSymbols.Concat(optionalSymbols))
            s.IsActive = activeSymbols.Contains(s.Name);

        foreach (var s in adProviderSymbols)
            s.IsActive = activeSymbols.Contains(s.Name);
    }

    // ─────────────────── Standard symbol ───────────────────
    [HideReferenceObjectPicker]
    public class SymbolState
    {
        [HideInInspector] public string Name;
        [HideInInspector] public bool IsActive;

        public SymbolState(string name) => Name = name;

        [HorizontalGroup("Row", PaddingLeft = 10, PaddingRight = 10)]
        [DisplayAsString, HideLabel]
        [ShowInInspector]
        public string SymbolName => Name;

        [HorizontalGroup("Row", Width = 100)]
        [Button("$ButtonLabel")]
        [GUIColor("$ButtonColor")]
        public virtual void Toggle()
        {
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            
            List<string> allDefines = defines.Split(';')
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            if (allDefines.Contains(Name)) 
                allDefines.Remove(Name);
            else 
                allDefines.Add(Name);

            PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", allDefines.ToArray()));
        }

        protected string ButtonLabel => IsActive ? "Active" : "Disabled";
        protected Color ButtonColor => IsActive ? Color.green : Color.red;
    }

    // ─────────────────── Mutually-exclusive symbol ───────────────────
    [HideReferenceObjectPicker]
    public class ExclusiveSymbolState : SymbolState
    {
        private readonly string[] _conflictingSymbols;

        public ExclusiveSymbolState(string name, string[] conflictingSymbols) : base(name)
        {
            _conflictingSymbols = conflictingSymbols;
        }

        [HorizontalGroup("Row", Width = 100)]
        [Button("$ButtonLabel")]
        [GUIColor("$ButtonColor")]
        public override void Toggle()
        {
            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

            List<string> allDefines = defines.Split(';')
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            bool isCurrentlyActive = allDefines.Contains(Name);

            if (isCurrentlyActive)
            {
                // Deactivate self
                allDefines.Remove(Name);
            }
            else
            {
                // Remove all conflicting providers before enabling this one
                foreach (var conflict in _conflictingSymbols)
                    allDefines.Remove(conflict);

                allDefines.Add(Name);
            }

            PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", allDefines.ToArray()));
        }
    }
}