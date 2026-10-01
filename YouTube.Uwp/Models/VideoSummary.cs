using System;
using System.ComponentModel;
using Windows.UI.Xaml;

namespace YouTube.Uwp.Models
{
    // Kept platform-independent so it can replace the WP8 ResultItem-based model safely.
    public class VideoSummary : INotifyPropertyChanged
    {
        private bool isExpanded;

        public string Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ChannelId { get; set; }

        public string ChannelTitle { get; set; }

        public string ThumbnailUrl { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }

        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (isExpanded == value)
                {
                    return;
                }

                isExpanded = value;
                OnPropertyChanged("IsExpanded");
                OnPropertyChanged("ThumbnailHeight");
                OnPropertyChanged("DescriptionVisibility");
                OnPropertyChanged("ExpandGlyph");
            }
        }

        public double ThumbnailHeight
        {
            get { return IsExpanded ? 260 : 148; }
        }

        public Visibility DescriptionVisibility
        {
            get { return IsExpanded ? Visibility.Visible : Visibility.Collapsed; }
        }

        public string ExpandGlyph
        {
            get { return IsExpanded ? "−" : "+"; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
