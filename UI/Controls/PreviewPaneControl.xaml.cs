using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using RiftVault.ViewModels;

namespace RiftVault.UI.Controls
{
    public partial class PreviewPaneControl : UserControl
    {
        private readonly DispatcherTimer _mediaTimer;
        private bool _isUserSeeking;
        private PreviewPaneViewModel? _currentVm;

        // Mouse Pan state for zoomed images
        private Point _lastPanPoint;
        private bool _isPanning;

        public event RoutedEventHandler? CloseRequested;

        public PreviewPaneControl()
        {
            InitializeComponent();

            _mediaTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _mediaTimer.Tick += MediaTimer_Tick;

            DataContextChanged += PreviewPaneControl_DataContextChanged;
            Unloaded += PreviewPaneControl_Unloaded;
        }

        private void PreviewPaneControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is PreviewPaneViewModel vm)
            {
                _currentVm = vm;
                _currentVm.PropertyChanged += ViewModel_PropertyChanged;
                UpdatePreviewVisibility();
            }
            else
            {
                _currentVm = null;
                StopMedia();
                UpdatePreviewVisibility();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PreviewPaneViewModel.PreviewType) ||
                e.PropertyName == nameof(PreviewPaneViewModel.HasSelection) ||
                e.PropertyName == nameof(PreviewPaneViewModel.MediaSource))
            {
                UpdatePreviewVisibility();
            }
            else if (e.PropertyName == nameof(PreviewPaneViewModel.IsLooping))
            {
                UpdateLoopPillState();
            }
        }

        private void UpdatePreviewVisibility()
        {
            StopMedia();

            if (_currentVm == null || !_currentVm.HasSelection)
            {
                EmptyStateContainer.Visibility = Visibility.Visible;
                ImagePreviewContainer.Visibility = Visibility.Collapsed;
                MediaPreviewContainer.Visibility = Visibility.Collapsed;
                CodeTextPreviewContainer.Visibility = Visibility.Collapsed;
                GenericPreviewContainer.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyStateContainer.Visibility = Visibility.Collapsed;
            ImagePreviewContainer.Visibility = _currentVm.PreviewType == PreviewContentType.Image ? Visibility.Visible : Visibility.Collapsed;
            MediaPreviewContainer.Visibility = (_currentVm.PreviewType == PreviewContentType.Video || _currentVm.PreviewType == PreviewContentType.Audio) ? Visibility.Visible : Visibility.Collapsed;
            CodeTextPreviewContainer.Visibility = _currentVm.PreviewType == PreviewContentType.CodeText ? Visibility.Visible : Visibility.Collapsed;
            GenericPreviewContainer.Visibility = (_currentVm.PreviewType == PreviewContentType.Generic || _currentVm.PreviewType == PreviewContentType.Archive) ? Visibility.Visible : Visibility.Collapsed;

            // Reset image scroll offset
            if (ImageScrollViewer != null)
            {
                ImageScrollViewer.ScrollToHorizontalOffset(0);
                ImageScrollViewer.ScrollToVerticalOffset(0);
            }

            // If media, auto-play with looping enabled by default
            if ((_currentVm.PreviewType == PreviewContentType.Video || _currentVm.PreviewType == PreviewContentType.Audio) && _currentVm.MediaSource != null)
            {
                try
                {
                    MediaPlayer.Source = _currentVm.MediaSource;
                    MediaPlayer.SpeedRatio = _currentVm.PlaybackSpeed;
                    MediaPlayer.Play();
                    _mediaTimer.Start();
                    PlayPauseBtn.Content = "\uE769"; // Pause glyph
                    if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Collapsed;
                    UpdateLoopPillState();
                }
                catch { }
            }
        }

        private void UpdateLoopPillState()
        {
            if (LoopPill != null && _currentVm != null)
            {
                LoopPill.BorderBrush = _currentVm.IsLooping
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("CardBorderBrush");
                LoopPill.Background = _currentVm.IsLooping
                    ? (Brush)FindResource("TabActiveBrush")
                    : (Brush)FindResource("PillBackgroundBrush");
            }
        }

        private void StopMedia()
        {
            try
            {
                _mediaTimer.Stop();
                MediaPlayer.Stop();
                MediaPlayer.Source = null;
                PlayPauseBtn.Content = "\uE768"; // Play glyph
                if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Visible;
            }
            catch { }
        }

        private void MediaTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isUserSeeking && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var dur = MediaPlayer.NaturalDuration.TimeSpan;
                var pos = MediaPlayer.Position;
                if (dur.TotalSeconds > 0)
                {
                    MediaSeekSlider.Value = pos.TotalSeconds / dur.TotalSeconds;
                }
                MediaTimeText.Text = $"{pos:mm\\:ss} / {dur:mm\\:ss}";
            }
        }

        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var dur = MediaPlayer.NaturalDuration.TimeSpan;
                MediaTimeText.Text = $"00:00 / {dur:mm\\:ss}";
            }
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (_currentVm?.IsLooping == true)
            {
                // Smooth continuous looping (essential for wallpapers & loops)
                MediaPlayer.Position = TimeSpan.Zero;
                MediaPlayer.Play();
                _mediaTimer.Start();
                PlayPauseBtn.Content = "\uE769";
                if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                MediaPlayer.Position = TimeSpan.Zero;
                MediaPlayer.Pause();
                _mediaTimer.Stop();
                PlayPauseBtn.Content = "\uE768";
                MediaSeekSlider.Value = 0;
                if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Visible;
            }
        }

        private void TogglePlayPause()
        {
            if (MediaPlayer.Source == null && _currentVm?.MediaSource != null)
            {
                MediaPlayer.Source = _currentVm.MediaSource;
            }

            if (PlayPauseBtn.Content.ToString() == "\uE769") // currently playing -> pause
            {
                MediaPlayer.Pause();
                _mediaTimer.Stop();
                PlayPauseBtn.Content = "\uE768";
                if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                MediaPlayer.Play();
                _mediaTimer.Start();
                PlayPauseBtn.Content = "\uE769";
                if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void VideoArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TogglePlayPause();
            e.Handled = true;
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
            _mediaTimer.Stop();
            MediaPlayer.Position = TimeSpan.Zero;
            MediaSeekSlider.Value = 0;
            PlayPauseBtn.Content = "\uE768";
            if (CenterPlayOverlay != null) CenterPlayOverlay.Visibility = Visibility.Visible;
        }

        private void RewindBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var newPos = MediaPlayer.Position - TimeSpan.FromSeconds(5);
                if (newPos < TimeSpan.Zero) newPos = TimeSpan.Zero;
                MediaPlayer.Position = newPos;
            }
        }

        private void ForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var dur = MediaPlayer.NaturalDuration.TimeSpan;
                var newPos = MediaPlayer.Position + TimeSpan.FromSeconds(5);
                if (newPos > dur) newPos = dur;
                MediaPlayer.Position = newPos;
            }
        }

        private void LoopBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.ToggleLoopCommand.Execute(null);
                UpdateLoopPillState();
            }
            e.Handled = true;
        }

        private void LoopToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateLoopPillState();
        }

        private void SpeedBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.CycleSpeed();
                MediaPlayer.SpeedRatio = _currentVm.PlaybackSpeed;
            }
            e.Handled = true;
        }

        private void MediaSeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserSeeking = true;
        }

        private void MediaSeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ApplySeek();
        }

        private void ApplySeek()
        {
            _isUserSeeking = false;
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var dur = MediaPlayer.NaturalDuration.TimeSpan;
                MediaPlayer.Position = TimeSpan.FromSeconds(Math.Clamp(MediaSeekSlider.Value, 0, 1) * dur.TotalSeconds);
            }
        }

        private void MediaSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var dur = MediaPlayer.NaturalDuration.TimeSpan;
                var target = TimeSpan.FromSeconds(Math.Clamp(MediaSeekSlider.Value, 0, 1) * dur.TotalSeconds);
                MediaTimeText.Text = $"{target:mm\\:ss} / {dur:mm\\:ss}";

                if (_isUserSeeking && Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    ApplySeek();
                }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MediaPlayer.Volume = VolumeSlider.Value;
            if (MuteBtn != null)
            {
                MuteBtn.Content = VolumeSlider.Value == 0 ? "\uE74F" : "\uE767";
            }
        }

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer.IsMuted)
            {
                MediaPlayer.IsMuted = false;
                MuteBtn.Content = "\uE767";
            }
            else
            {
                MediaPlayer.IsMuted = true;
                MuteBtn.Content = "\uE74F";
            }
        }

        // ─── Image Zoom & Pan Handlers ───────────────────────────────────
        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentVm != null)
            {
                if (e.Delta > 0)
                    _currentVm.ZoomInCommand.Execute(null);
                else
                    _currentVm.ZoomOutCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void ImageScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _currentVm?.ZoomLevel > 1.0)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(ImageScrollViewer);
                ImageScrollViewer.CaptureMouse();
                ImageScrollViewer.Cursor = Cursors.SizeAll;
            }
        }

        private void ImageScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point curPoint = e.GetPosition(ImageScrollViewer);
                double deltaX = curPoint.X - _lastPanPoint.X;
                double deltaY = curPoint.Y - _lastPanPoint.Y;
                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - deltaX);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - deltaY);
                _lastPanPoint = curPoint;
            }
        }

        private void ImageScrollViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ImageScrollViewer.ReleaseMouseCapture();
                ImageScrollViewer.Cursor = Cursors.Arrow;
            }
        }

        private void ImageScrollViewer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_currentVm != null)
            {
                if (_currentVm.ZoomLevel > 1.0)
                    _currentVm.ResetZoomCommand.Execute(null);
                else
                    _currentVm.ZoomLevel = 2.0;
                e.Handled = true;
            }
        }

        // ─── Header Actions ──────────────────────────────────────────────
        private void InvestigateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentVm != null && !string.IsNullOrEmpty(_currentVm.FilePath) && File.Exists(_currentVm.FilePath))
            {
                try
                {
                    var invWindow = (UI.InvestigatorWindow)((App)Application.Current).Services.GetService(typeof(UI.InvestigatorWindow))!;
                    invWindow.InspectTarget(_currentVm.FilePath);
                    invWindow.Show();
                    invWindow.Activate();
                }
                catch { }
            }
        }

        private void ClosePreview_Click(object sender, RoutedEventArgs e)
        {
            StopMedia();
            CloseRequested?.Invoke(this, e);
        }

        // ─── Code / Text Editor Handlers ─────────────────────────────────
        private void CodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentVm != null && _currentVm.IsEditingText)
            {
                _currentVm.IsTextDirty = true;
            }
        }

        private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.S)
            {
                _ = _currentVm?.SaveTextAsync();
                e.Handled = true;
            }
        }

        private void PreviewPaneControl_Unloaded(object sender, RoutedEventArgs e)
        {
            StopMedia();
        }
    }
}
