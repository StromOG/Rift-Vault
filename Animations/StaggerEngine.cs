using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace RiftVault.Animations
{
    public static class StaggerEngine
    {
        public static async Task AnimateListAsync(IEnumerable<UIElement> elements, int staggerDelayMs = 30)
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                foreach (var el in elements)
                {
                    el.Opacity = 1;
                    if (el.RenderTransform is System.Windows.Media.TranslateTransform tt) tt.Y = 0;
                }
                return;
            }

            foreach (var element in elements)
            {
                element.Opacity = 0;
                if (element.RenderTransform is not System.Windows.Media.TranslateTransform)
                {
                    element.RenderTransform = new System.Windows.Media.TranslateTransform { Y = 20 };
                }
                else
                {
                    ((System.Windows.Media.TranslateTransform)element.RenderTransform).Y = 20;
                }
            }

            foreach (var element in elements)
            {
                TransitionEngine.FadeInUp(element, 20);
                await Task.Delay(staggerDelayMs);
            }
        }
    }
}