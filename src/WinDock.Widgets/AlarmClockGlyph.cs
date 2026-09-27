using System.Windows;
using System.Windows.Media;
using WinDock.Core;

namespace WinDock.Widgets;

/// <summary>
/// Fluent UI System Icons “Clock Alarm” (24 regular / filled), scaled to the tile.
/// Source: https://github.com/microsoft/fluentui-system-icons
/// Copyright (c) 2020 Microsoft Corporation
///
/// MIT License
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.
/// </summary>
public sealed class AlarmClockGlyph : FrameworkElement
{
    private const double Design = 24;

    private static readonly Geometry Regular = Parse(RegularPath);
    private static readonly Geometry FilledIcon = Parse(FilledPath);

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(AlarmClockGlyph),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled),
        typeof(bool),
        typeof(AlarmClockGlyph),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public AlarmClockGlyph(double size)
    {
        Width = size;
        Height = size;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        IsHitTestVisible = false;
        Stroke = TokenBrushes.From(DockTokens.Dark.Text);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var scale = Math.Min(w, h) / Design;
        var ox = (w - Design * scale) / 2;
        var oy = (h - Design * scale) / 2;
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, ox, oy));
        dc.DrawGeometry(Stroke ?? Brushes.White, null, Filled ? FilledIcon : Regular);
        dc.Pop();
    }

    private static Geometry Parse(string path)
    {
        var geometry = Geometry.Parse("F1 " + path);
        geometry.Freeze();
        return geometry;
    }

    // ic_fluent_clock_alarm_24_regular.svg
    private const string RegularPath =
        "M12 7.75C12 7.33579 11.6642 7 11.25 7C10.8358 7 10.5 7.33579 10.5 7.75V12.75C10.5 13.1642 10.8358 13.5 11.25 13.5H14.75C15.1642 13.5 15.5 13.1642 15.5 12.75C15.5 12.3358 15.1642 12 14.75 12H12V7.75ZM3.47628 9.10342C2.57641 8.37072 2 7.25296 2 6C2 3.79086 3.79086 2 6 2C7.25298 2 8.37063 2.57632 9.1034 3.47628C10.0124 3.16748 10.9866 3 12 3C13.0134 3 13.9877 3.16749 14.8967 3.4763C15.6294 2.57634 16.7471 2 18.0001 2C20.2092 2 22.0001 3.79086 22.0001 6C22.0001 7.253 21.4237 8.37072 20.5237 9.10346C20.8325 10.0124 21 10.9866 21 12C21 14.2153 20.1996 16.2436 18.8723 17.8116L20.7803 19.7197C21.0732 20.0126 21.0732 20.4874 20.7803 20.7803C20.4874 21.0732 20.0126 21.0732 19.7197 20.7803L17.8116 18.8723C16.2436 20.1996 14.2153 21 12 21C9.78472 21 7.75638 20.1996 6.18837 18.8723L4.28033 20.7803C3.98744 21.0732 3.51256 21.0732 3.21967 20.7803C2.92678 20.4874 2.92678 20.0126 3.21967 19.7197L5.12771 17.8116C3.80037 16.2436 3 14.2153 3 12C3 10.9866 3.16748 10.0124 3.47628 9.10342ZM3.5 6C3.5 6.63099 3.73356 7.20781 4.12013 7.64828C4.94029 6.16634 6.16635 4.94029 7.64828 4.12013C7.20784 3.73361 6.63096 3.5 6 3.5C4.61929 3.5 3.5 4.61929 3.5 6ZM19.8799 7.64833C20.2665 7.20787 20.5001 6.631 20.5001 6C20.5001 4.61929 19.3808 3.5 18.0001 3.5C17.3691 3.5 16.7922 3.73361 16.3518 4.12016C17.8337 4.94033 19.0598 6.16639 19.8799 7.64833ZM4.5 12C4.5 16.1421 7.85786 19.5 12 19.5C16.1421 19.5 19.5 16.1421 19.5 12C19.5 7.85786 16.1421 4.5 12 4.5C7.85786 4.5 4.5 7.85786 4.5 12Z";

    // ic_fluent_clock_alarm_24_filled.svg
    private const string FilledPath =
        "M9.1034 3.47628C8.37063 2.57632 7.25298 2 6 2C3.79086 2 2 3.79086 2 6C2 7.25296 2.57641 8.37072 3.47628 9.10342C3.16748 10.0124 3 10.9866 3 12C3 14.2153 3.80037 16.2436 5.12771 17.8116L3.21967 19.7197C2.92678 20.0126 2.92678 20.4874 3.21967 20.7803C3.51256 21.0732 3.98744 21.0732 4.28033 20.7803L6.18837 18.8723C7.75638 20.1996 9.78472 21 12 21C14.2153 21 16.2436 20.1996 17.8116 18.8723L19.7197 20.7803C20.0126 21.0732 20.4874 21.0732 20.7803 20.7803C21.0732 20.4874 21.0732 20.0126 20.7803 19.7197L18.8723 17.8116C20.1996 16.2436 21 14.2153 21 12C21 10.9866 20.8325 10.0124 20.5237 9.10346C21.4237 8.37072 22.0001 7.253 22.0001 6C22.0001 3.79086 20.2092 2 18.0001 2C16.7471 2 15.6294 2.57634 14.8967 3.4763C13.9877 3.16749 13.0134 3 12 3C10.9866 3 10.0124 3.16748 9.1034 3.47628ZM6 3.5C6.63096 3.5 7.20784 3.73361 7.64828 4.12013C6.16635 4.94029 4.94029 6.16634 4.12013 7.64828C3.73356 7.20781 3.5 6.63099 3.5 6C3.5 4.61929 4.61929 3.5 6 3.5ZM16.3518 4.12016C16.7922 3.73361 17.3691 3.5 18.0001 3.5C19.3808 3.5 20.5001 4.61929 20.5001 6C20.5001 6.631 20.2665 7.20788 19.8799 7.64834C19.0598 6.1664 17.8337 4.94033 16.3518 4.12016ZM11.25 7C11.6642 7 12 7.33579 12 7.75V12H14.75C15.1642 12 15.5 12.3358 15.5 12.75C15.5 13.1642 15.1642 13.5 14.75 13.5H11.25C10.8358 13.5 10.5 13.1642 10.5 12.75V7.75C10.5 7.33579 10.8358 7 11.25 7Z";
}
