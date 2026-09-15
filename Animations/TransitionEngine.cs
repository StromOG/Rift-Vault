using System.Windows;

namespace RiftVault.Animations
{
    public static class TransitionEngine
    {
        public static void SlideInFromRight(UIElement element, double offset = 100)
        {
            element.Opacity = 0;
            SpringEngine.AnimateTranslateX(element, offset, SpringConfig.UI);
            
            // Reset and animate to 0
            SpringEngine.AnimateDouble(offset, 0, SpringConfig.Gentle, val => 
            {
                if (element.RenderTransform is System.Windows.Media.TranslateTransform tt)
                    tt.X = val;
            });
            SpringEngine.AnimateOpacity(element, 1.0, SpringConfig.Gentle);
        }

        public static void FadeInUp(UIElement element, double offset = 30)
        {
            element.Opacity = 0;
            SpringEngine.AnimateTranslateY(element, offset, SpringConfig.UI);
            
            SpringEngine.AnimateDouble(offset, 0, SpringConfig.Gentle, val => 
            {
                if (element.RenderTransform is System.Windows.Media.TranslateTransform tt)
                    tt.Y = val;
            });
            SpringEngine.AnimateOpacity(element, 1.0, SpringConfig.Gentle);
        }

        public static void PopIn(UIElement element)
        {
            element.Opacity = 0;
            SpringEngine.AnimateScale(element, 0.8, SpringConfig.UI);

            SpringEngine.AnimateScale(element, 1.0, SpringConfig.Bounce);
            SpringEngine.AnimateOpacity(element, 1.0, SpringConfig.Snappy);
        }
    }
}