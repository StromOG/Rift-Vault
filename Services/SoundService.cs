using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace RiftVault.Services
{
    public interface ISoundService
    {
        void PlaySound(SoundEvent soundEvent);
    }

    public enum SoundEvent
    {
        Navigation,
        FileOpen,
        CopyComplete,
        Delete,
        Error
    }

    public class SoundService : ISoundService
    {
        private readonly ISettingsService _settingsService;
        private readonly string _soundsDir;

        public SoundService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            // In a real app, sounds are extracted from embedded resources to AppData
            _soundsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RiftVault", "sounds");
            Directory.CreateDirectory(_soundsDir);
        }

        public void PlaySound(SoundEvent soundEvent)
        {
            if (!_settingsService.Current.EnableUISounds) return;

            string fileName = soundEvent switch
            {
                SoundEvent.Navigation => "nav.wav",
                SoundEvent.FileOpen => "open.wav",
                SoundEvent.CopyComplete => "success.wav",
                SoundEvent.Delete => "trash.wav",
                SoundEvent.Error => "error.wav",
                _ => ""
            };

            string path = Path.Combine(_soundsDir, fileName);
            if (!File.Exists(path)) return;

            Task.Run(() =>
            {
                try
                {
                    using var audioFile = new AudioFileReader(path);
                    using var outputDevice = new WaveOutEvent();
                    
                    audioFile.Volume = (float)(_settingsService.Current.SoundVolume / 100.0);
                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    
                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
                catch { /* Ignore audio playback errors */ }
            });
        }
    }
}