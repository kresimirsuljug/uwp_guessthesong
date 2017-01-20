using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UWPGuessSong.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPGuessSong
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<Song> Songs;
        private ObservableCollection<StorageFile> AllSongs;
        bool _playingMusic = false;
        int _round = 0;
        int _totalscore= 0;

        public MainPage()
        {
            this.InitializeComponent();
            Songs = new ObservableCollection<Song>();
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
       
           
        }
    
        private async Task<ObservableCollection<StorageFile>> SetupMusicList()
        {
            StorageFolder folder = KnownFolders.MusicLibrary;
            var allSongs = new ObservableCollection<StorageFile>();
            await RetriveFilesInFolders(allSongs, folder);
            return allSongs;
        }
      

        private async  Task RetriveFilesInFolders(ObservableCollection<StorageFile> list, StorageFolder parent)
        {
           foreach (var item in await parent.GetFilesAsync())
            {
                if(item.FileType == ".mp3")
                {
                    list.Add(item);
                }
            }
           foreach (var item in await parent.GetFoldersAsync())
            {
                await RetriveFilesInFolders(list, item);
            }

        }
        
        private async Task< List<StorageFile>> PickRandomSongs(ObservableCollection<StorageFile> allSongs)
        {
            Random random = new Random();
            var randomSongs = new List<StorageFile>();
            var songCount = allSongs.Count;
            while (randomSongs.Count < 10)
            {
                var randomNumber = random.Next(songCount);
                var randomSong = allSongs[randomNumber];
                MusicProperties randomSongMusicProperties = await randomSong.Properties.GetMusicPropertiesAsync();
                bool isDuplicate = false;
                foreach ( var song in randomSongs)
                {
                    MusicProperties songMusicProperties = await song.Properties.GetMusicPropertiesAsync();
                    if(String.IsNullOrEmpty(randomSongMusicProperties.Album) || randomSongMusicProperties.Album == songMusicProperties.Album)
                    {
                        isDuplicate = true;
                    }
                }
                if (!isDuplicate)
                { 
                    randomSongs.Add(randomSong);
                }

                
            }
            return randomSongs;
        }

        private async Task  PopulateSongList(List<StorageFile> files)
        {
            int id = 0;
            foreach(var file in files)
            {
                MusicProperties songProperties = await file.Properties.GetMusicPropertiesAsync();
                StorageItemThumbnail currentThumb = await file.GetThumbnailAsync(ThumbnailMode.MusicView, 200, ThumbnailOptions.UseCurrentScale);
                var albumCover = new BitmapImage();
                albumCover.SetSource(currentThumb);
                var song = new Song();
                song.id = id;
                song.Title = songProperties.Title;
                song.Artist = songProperties.Artist;
                song.Album = songProperties.Album;
                song.AlbumCover = albumCover;
                song.SongFile = file;
                Songs.Add(song);
                id++;


            }
        }

        private async void SongGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!_playingMusic) return;
            CountDown.Pause();
            MyMediaElement.Stop();

            var clickedSong = (Song)e.ClickedItem;
            var correctSong = Songs.FirstOrDefault(p => p.Selected == true);
            int score;
            Uri uri;
            if (clickedSong.Selected)
            {
                uri = new Uri("ms-appx:///Assets/correct.png");
                score =  (int)MyProgressBar.Value;
            }
            else
            {
                uri = new Uri("ms-appx:///Assets/incorrect.png");
                score = 0;
            }
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var fileStream = await file.OpenAsync(FileAccessMode.Read);
            await clickedSong.AlbumCover.SetSourceAsync(fileStream);
            _totalscore += score;
            _round++;
            ResultTextBlock.Visibility = Visibility.Visible;
            TitleTextBlock.Visibility = Visibility.Visible;
            ArtistTextBlock.Visibility = Visibility.Visible;
            AlbumTextBlock.Visibility = Visibility.Visible;
            ResultTextBlock.Text = string.Format("Score: {0} Total Score after {1} rounds : {2}", score, _round, _totalscore);
            TitleTextBlock.Text = string.Format("Correct song {0}", correctSong.Title);
            ArtistTextBlock.Text = string.Format("Performed by: {0}", correctSong.Artist);
            AlbumTextBlock.Text = string.Format("On Album: {0}", correctSong.Album);


            clickedSong.Used = true;
            correctSong.Selected = false;
            correctSong.Used = true;
            if(_round >=5)
            {
                InstructionTextBlock.Text = string.Format("Game Over... your score is : {0}", _totalscore);
                ResultTextBlock.Visibility = Visibility.Collapsed;
                TitleTextBlock.Visibility = Visibility.Collapsed;
                ArtistTextBlock.Visibility = Visibility.Collapsed;
                AlbumTextBlock.Visibility = Visibility.Collapsed;
                PlayAgainButton.Visibility = Visibility.Visible;
            }
            else
            {
                StartCooldown();
            }
            
        }

        private async void PlayAgainButton_Click(object sender, RoutedEventArgs e)
        {
            PlayAgainButton.Visibility = Visibility.Collapsed;
            _round = 0;
            _totalscore = 0;
         
            await PrepareNewGame();
            StartCooldown();
        }
        private async Task PrepareNewGame()
        {
            Songs.Clear();
            var randomSongs = await PickRandomSongs(AllSongs);
            await PopulateSongList(randomSongs);
        }
        private async void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            StartupProgressRing.IsActive = true;            
            AllSongs = await SetupMusicList();
            await PrepareNewGame();
            StartupProgressRing.IsActive = false;
            StartCooldown();
        }
        private void StartCooldown()
        {
            _playingMusic = false;
            SolidColorBrush brush = new SolidColorBrush(Colors.Blue);
            MyProgressBar.Foreground = brush;
            InstructionTextBlock.Text = string.Format("Get ready for Round {0}...", _round+1);
            InstructionTextBlock.Foreground = brush;
            CountDown.Begin();
        }
        private void StartCountDown()
        {
            _playingMusic = true;
            SolidColorBrush brush = new SolidColorBrush(Colors.Red);
            MyProgressBar.Foreground = brush;
            InstructionTextBlock.Text = "Go!";
            InstructionTextBlock.Foreground = brush;
            CountDown.Begin();
        }
        private async void CountDown_Completed(object sender, object e)
        {
            if (!_playingMusic)
            {
                var song = PickSong();
              
                MyMediaElement.SetSource(await song.SongFile.OpenAsync(FileAccessMode.Read), song.SongFile.ContentType);

                StartCountDown();
            }
        }
        private Song PickSong()
        {
            Random random = new Random();
            var unusedSong = Songs.Where(p => p.Used == false);
            var randomNumber = random.Next(unusedSong.Count());
            var randomSong = unusedSong.ElementAt(randomNumber);
            randomSong.Selected = true;
            return randomSong;
        }
    }
}
