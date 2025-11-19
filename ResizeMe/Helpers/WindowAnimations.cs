using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Composition;
using System;

namespace ResizeMe.Helpers
{
    internal static class WindowAnimations
    {
        public static void AnimateShow(UIElement element)
        {
            if (element == null) return;
            try
            {
                element.Opacity = 0;
                var animation = element.CreateDoubleAnimation(0, 1, 180);
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;
                using var batch = compositor.CreateBatch();
                visual.StartAnimation("Opacity", animation);
                batch.Completed += (_, _) => element.DispatcherQueue.TryEnqueue(() => element.Opacity = 1);
            }
            catch { }
        }

        public static void AnimateHide(UIElement element)
        {
            if (element == null) return;
            try
            {
                var animation = element.CreateDoubleAnimation(element.Opacity, 0, 120);
                var visual = ElementCompositionPreview.GetElementVisual(element);
                var compositor = visual.Compositor;
                using var batch = compositor.CreateBatch();
                visual.StartAnimation("Opacity", animation);
                batch.Completed += (_, _) => element.DispatcherQueue.TryEnqueue(() => element.Opacity = 0);
            }
            catch { }
        }
    }
}
