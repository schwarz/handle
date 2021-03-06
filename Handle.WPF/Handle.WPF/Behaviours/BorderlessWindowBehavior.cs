﻿// -----------------------------------------------------------------------
// <copyright file="BorderlessWindowBehavior.xaml.cs" company="">
// Microsoft Public License (Ms-PL)
// http://www.opensource.org/licenses/MS-PL
// </copyright>
// -----------------------------------------------------------------------

namespace Handle.WPF.Behaviours
{
  using System;
  using System.Runtime.InteropServices;
  using System.Windows;
  using System.Windows.Controls;
  using System.Windows.Interactivity;
  using System.Windows.Interop;
  using System.Windows.Media;
  using Handle.WPF.Controls;
  using Handle.WPF.Native;

  public class BorderlessWindowBehavior : Behavior<Window>
  {
    [DllImport("dwmapi", PreserveSig = false)]
    public static extern bool DwmIsCompositionEnabled();

    [DllImport("dwmapi")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("user32")]
    public static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32")]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

    [DllImport("user32")]
    internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    private HwndSource m_hwndSource;
    private IntPtr m_hwnd;

    public static DependencyProperty ResizeWithGripProperty = DependencyProperty.Register("ResizeWithGrip", typeof(bool), typeof(BorderlessWindowBehavior), new PropertyMetadata(true));
    public static DependencyProperty AutoSizeToContentProperty = DependencyProperty.Register("AutoSizeToContent", typeof(bool), typeof(BorderlessWindowBehavior), new PropertyMetadata(false));

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {

      var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

      // Adjust the maximized size and position to fit the work area of the correct monitor

      System.IntPtr monitor = MonitorFromWindow(hwnd, Constants.MONITOR_DEFAULTTONEAREST);

      if (monitor != System.IntPtr.Zero)
      {

        MONITORINFO monitorInfo = new MONITORINFO();
        GetMonitorInfo(monitor, monitorInfo);
        RECT rcWorkArea = monitorInfo.rcWork;
        RECT rcMonitorArea = monitorInfo.rcMonitor;
        mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
        mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
        mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
        mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
      }

      Marshal.StructureToPtr(mmi, lParam, true);
    }

    public bool ResizeWithGrip
    {
      get { return (bool)GetValue(ResizeWithGripProperty); }
      set { SetValue(ResizeWithGripProperty, value); }
    }

    public bool AutoSizeToContent
    {
      get { return (bool)GetValue(AutoSizeToContentProperty); }
      set { SetValue(AutoSizeToContentProperty, value); }
    }

    public Border Border { get; set; }

    protected override void OnAttached()
    {
      if (HwndSource.FromVisual(AssociatedObject) != null)
        AddHwndHook();
      else
        AssociatedObject.SourceInitialized += AssociatedObject_SourceInitialized;

      AssociatedObject.WindowStyle = WindowStyle.None;
      AssociatedObject.ResizeMode = ResizeWithGrip ? ResizeMode.CanResizeWithGrip : ResizeMode.CanResize;

      if (AssociatedObject is MetroWindow)
      {
        //MetroWindow already has a border we can use
        AssociatedObject.Loaded += (s, e) =>
        {
          var ancestors = ((MetroWindow)AssociatedObject).GetPart<Border>("PART_Border");
          Border = ancestors;
        };
      }
      else
      {
        //Other windows may not, easiest to just inject one!
        var content = (UIElement)AssociatedObject.Content;
        AssociatedObject.Content = null;

        Border = new Border
        {
          Child = content,
          BorderBrush = new SolidColorBrush(Colors.Black)
        };

        AssociatedObject.Content = Border;
      }
      if (AutoSizeToContent)
        AssociatedObject.Loaded += (s, e) =>
        {
          //Temp fix, thanks @lynnx
          AssociatedObject.SizeToContent = SizeToContent.Height;
          AssociatedObject.SizeToContent = AutoSizeToContent
                                               ? SizeToContent.WidthAndHeight
                                               : SizeToContent.Manual;
        };

      base.OnAttached();
    }

    protected override void OnDetaching()
    {
      RemoveHwndHook();
      base.OnDetaching();
    }

    private void AddHwndHook()
    {
      m_hwndSource = HwndSource.FromVisual(AssociatedObject) as HwndSource;
      m_hwndSource.AddHook(HwndHook);
      m_hwnd = new WindowInteropHelper(AssociatedObject).Handle;
    }

    private void RemoveHwndHook()
    {
      AssociatedObject.SourceInitialized -= AssociatedObject_SourceInitialized;
      m_hwndSource.RemoveHook(HwndHook);
    }

    private void AssociatedObject_SourceInitialized(object sender, EventArgs e)
    {
      AddHwndHook();
    }

    private IntPtr HwndHook(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
      IntPtr returnval = IntPtr.Zero;
      switch (message)
      {
        case Constants.WM_CREATE:
          handled = true;
          var create = (CREATESTRUCT)Marshal.PtrToStructure(lParam, typeof(CREATESTRUCT));
          if (create.style == Constants.WS_MAXIMIZE)
          {
            MessageBox.Show("Got here");
          }
          break;

        case Constants.WM_NCCALCSIZE:
          /* Hides the border */
          handled = true;
          break;
        case Constants.WM_NCPAINT:
          {
            if (Environment.OSVersion.Version.Major >= 6 && DwmIsCompositionEnabled())
            {
              var m = new MARGINS { bottomHeight = 1, leftWidth = 1, rightWidth = 1, topHeight = 1 };
              DwmExtendFrameIntoClientArea(m_hwnd, ref m);
              if (Border != null)
                Border.BorderThickness = new Thickness(0);
            }
            else
            {
              if (Border != null)
                Border.BorderThickness = new Thickness(1);
            }
            handled = true;
          }
          break;
        case Constants.WM_NCACTIVATE:
          {
            /* As per http://msdn.microsoft.com/en-us/library/ms632633(VS.85).aspx , "-1" lParam
             * "does not repaint the nonclient area to reflect the state change." */
            returnval = DefWindowProc(hWnd, message, wParam, new IntPtr(-1));
            handled = true;
          }
          break;
        case Constants.WM_GETMINMAXINFO:
          /* http://blogs.msdn.com/b/llobo/archive/2006/08/01/maximizing-window-_2800_with-windowstyle_3d00_none_2900_-considering-taskbar.aspx */
          WmGetMinMaxInfo(hWnd, lParam);

          /* Setting handled to false enables the application to process it's own Min/Max requirements,
           * as mentioned by jason.bullard (comment from September 22, 2011) on http://gallery.expression.microsoft.com/ZuneWindowBehavior/ */
          handled = false;
          break;
      }


      return returnval;
    }
  }
}
