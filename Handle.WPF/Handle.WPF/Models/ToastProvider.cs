﻿// -----------------------------------------------------------------------
// <copyright file="ToastProvider.cs" company="">
// Copyright (c) 2011-2012 Bernhard Schwarz, Florian Lembeck
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// -----------------------------------------------------------------------

namespace Handle.WPF
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading;
  using System.Windows;
  using System.Windows.Threading;
  using Caliburn.Micro;

  /// <summary>
  /// A class representing a INotificationProvider implementation to show toasts.
  /// </summary>
  class ToastProvider : INotificationProvider
  {
    private Screen screen;
    private IWindowManager windowManager;

    public ToastProvider(Screen s)
    {
      this.screen = s;
      try
      {
        this.windowManager = IoC.Get<IWindowManager>();
      }
      catch
      {
        this.windowManager = new WindowManager();
      }
    }

    /// <summary>
    /// Notifies the user by displaying a toast in the bottom right corner
    /// </summary>
    /// <param name="e">Contains the channel, user, etc. of the important message</param>
    public void Notify(MessageFilterEventArgs e)
    {
      Window x = screen.GetView() as Window;
      var ntvm = new NotificationToastViewModel(e, x);
      x.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
      {
        this.windowManager.ShowWindow(ntvm);
      }));
    }
  }
}
