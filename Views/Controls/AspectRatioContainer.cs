namespace Re_RunApp.Views.Controls;

using Microsoft.Maui.Controls;
using System;

public class AspectRatioContainer : ContentView
{
    public static readonly BindableProperty RatioProperty =
        BindableProperty.Create(nameof(Ratio), typeof(double), typeof(AspectRatioContainer), 1.0);

    /// <summary>
    /// Height = Width * Ratio
    /// </summary>
    public double Ratio
    {
        get => (double)GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && Ratio > 0)
        {
            var desired = width * Ratio;
            // Only set HeightRequest if it differs significantly to avoid layout thrash
            if (Math.Abs(HeightRequest - desired) > 0.5)
            {
                HeightRequest = desired;
            }
        }
    }
}
