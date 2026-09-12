using System;
using System.Windows.Input;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Вкладка: что показывать и как она называется.
    ///
    /// Содержимым может быть любая модель экрана — список постановлений,
    /// редактор записи, наряд. Сама вкладка про них ничего не знает: какой
    /// разметкой рисовать модель, решает слой View.
    /// </summary>
    public class DocumentViewModel : ViewModelBase
    {
        private string _title;

        public DocumentViewModel(object content, string title, string key)
        {
            Content = content;
            _title = title;
            Key = key;

            CloseCommand = new RelayCommand(o => RequestClose());
        }

        /// <summary>Модель экрана.</summary>
        public object Content { get; private set; }

        /// <summary>
        /// Чем вкладка отличается от других. Разделы открываются по одному —
        /// второй щелчок по «Штрафам» должен показать уже открытую вкладку,
        /// а не завести вторую такую же. У редакторов ключ свой у каждого.
        /// </summary>
        public string Key { get; private set; }

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public ICommand CloseCommand { get; private set; }

        /// <summary>Вкладку просят закрыть.</summary>
        public event EventHandler Closing;

        public void RequestClose()
        {
            var handler = Closing;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
