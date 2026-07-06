using UnityEngine;

namespace AllIn1SpringsToolkit.Demo.Scripts
{
    public class SetFrameRate : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool vsync = false;
        [SerializeField] private bool onUpdate = false;
        
        private void Start()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
        }

        private void Update()
        {
            if(onUpdate)
            {
                Application.targetFrameRate = targetFrameRate;   
            }
        }
    }
}