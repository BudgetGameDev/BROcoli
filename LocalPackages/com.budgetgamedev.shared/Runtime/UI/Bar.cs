using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared
{
    public class Bar : MonoBehaviour
    {
        [SerializeField]
        private Slider slider;

        public void UpdateBar(float current, float total)
        {
            ResolveSlider();
            if (slider == null)
                return;

            float value = total > 0f ? Mathf.Clamp01(current / total) : 0f;
            slider.SetValueWithoutNotify(value);
        }

        public void ShowBar()
        {
            gameObject.SetActive(true);
        }

        public void HideBar()
        {
            gameObject.SetActive(false);
        }

        private void ResolveSlider()
        {
            if (slider == null)
                slider = GetComponent<Slider>();
        }
    }
}
