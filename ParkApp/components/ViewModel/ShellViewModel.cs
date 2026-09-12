using System.Collections.ObjectModel;
using System.Linq;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Набор вкладок одного окна.
    ///
    /// Окон может быть несколько: вкладку вытаскивают мышью, и она становится
    /// отдельным окном со своим набором. Окна ничего друг о друге не знают —
    /// поэтому «оболочка» это не одиночка, а обычный объект, свой у каждого окна.
    /// </summary>
    public class ShellViewModel : ViewModelBase
    {
        private DocumentViewModel _selected;

        public ShellViewModel()
        {
            Documents = new ObservableCollection<DocumentViewModel>();
        }

        public ShellViewModel(DocumentViewModel document)
            : this()
        {
            Add(document);
        }

        public ObservableCollection<DocumentViewModel> Documents { get; private set; }

        public DocumentViewModel Selected
        {
            get { return _selected; }
            set { SetProperty(ref _selected, value); }
        }

        public bool IsEmpty
        {
            get { return Documents.Count == 0; }
        }

        /// <summary>
        /// Открывает вкладку. Если такая уже есть — просто показывает её:
        /// два одинаковых списка постановлений рядом только путают.
        /// </summary>
        public DocumentViewModel Open(object content, string title, string key)
        {
            var existing = key == null
                ? null
                : Documents.FirstOrDefault(d => d.Key == key);

            if (existing != null)
            {
                Selected = existing;
                return existing;
            }

            return Add(new DocumentViewModel(content, title, key));
        }

        public DocumentViewModel Add(DocumentViewModel document)
        {
            if (document == null)
                return null;

            document.Closing += (sender, args) => Close(document);

            Documents.Add(document);
            Selected = document;
            OnPropertyChanged("IsEmpty");

            return document;
        }

        public void Close(DocumentViewModel document)
        {
            if (document == null || !Documents.Contains(document))
                return;

            var index = Documents.IndexOf(document);
            Documents.Remove(document);

            if (Documents.Count > 0)
                Selected = Documents[System.Math.Min(index, Documents.Count - 1)];

            OnPropertyChanged("IsEmpty");
        }

        /// <summary>Убирает вкладку, не закрывая её: она уезжает в другое окно.</summary>
        public DocumentViewModel Detach(DocumentViewModel document)
        {
            if (document == null || !Documents.Contains(document))
                return null;

            Close(document);
            return document;
        }
    }
}
