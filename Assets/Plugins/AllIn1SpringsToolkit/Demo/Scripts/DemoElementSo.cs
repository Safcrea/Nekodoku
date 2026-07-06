using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    [CreateAssetMenu(fileName = "NewDemoElement", menuName = "AllIn1SpringsToolkit/Demo/DemoElement")]
    public class DemoElementSo : ScriptableObject
    {
        public DemoElement demoElement;
        [TextArea(3, 10)] public string description;
        public bool is3d;
        public GameObject buttonPrefabOf3dElement;
    }
}