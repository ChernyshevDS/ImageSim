using System.Windows;

namespace ImageSim.ViewModels
{
    /// <summary>
    /// Класс-прокси для биндинга в условиях отсутствия DataContext (например, внутри ContextMenu)
    /// https://thomaslevesque.com/2011/03/21/wpf-how-to-bind-to-data-when-the-datacontext-is-not-inherited/
    /// </summary>
    public class BindingProxy : Freezable
    {
        #region Overrides of Freezable
        /// <summary>
        /// Создать экземпляр
        /// </summary>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        #endregion

        /// <summary>
        /// Прокси-свойство
        /// </summary>
        public object Data
        {
            get { return (object)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        /// <summary>
        /// Прокси-свойство
        /// </summary>
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    }
}
