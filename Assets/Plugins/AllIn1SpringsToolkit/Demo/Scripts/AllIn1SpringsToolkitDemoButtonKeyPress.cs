using AllIn1SpringsToolkit.Utils;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class AllIn1SpringsToolkitDemoButtonKeyPress : MonoBehaviour
    {
        [SerializeField] private KeyCode targetKey = KeyCode.A;
        [SerializeField] private AllIn1SpringsToolkitSimpleButton simpleButton;

        [Space, Header("Label Settings")]
        [SerializeField] private bool completelyIgnoreLabel;
        [SerializeField] private bool showKeyLabel = true;
        [SerializeField] private TextMeshProUGUI keyLabel;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private Key targetNewInputKey;
        private void Awake()
        {
            targetNewInputKey = ConvertKeyCodeToKey(targetKey);
        }
#endif

        private void Start()
        {
            if(!completelyIgnoreLabel)
            {
                if(!showKeyLabel)
                {
                    keyLabel.enabled = false;
                    enabled = false;
                }
                else
                {
                    keyLabel.text = $"(Key {targetKey.ToString()})";
                }
            }
        }

        private void Update()
        {
            if(IsTargetKeyPressed())
            {
                simpleButton.SimulateClick();
            }
        }

        private bool IsTargetKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Keyboard.current != null && Keyboard.current[targetNewInputKey].wasPressedThisFrame;
#else
            return Input.GetKeyDown(targetKey);
#endif
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        //The KeyCode enum is not available in the new Input System, so we need to convert it to Key
        private Key ConvertKeyCodeToKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.None: return Key.None;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                // Add more special cases as needed
            }
            
            // Try to parse the KeyCode name to Key
            if(System.Enum.TryParse(keyCode.ToString(), true, out Key key))
            {
                return key;
            }

            Debug.LogWarning($"Failed to convert KeyCode {keyCode} to Key. Using default Key.None.");
            return Key.None;
        }
#endif
    }
}