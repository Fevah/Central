using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Calendar;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Calendar
{
    [Widget("Calendar", "Scheduler", "Displays Appointments in a calendar view.")]
    public partial class CalendarView
    {
        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public CalendarView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model
        /// </summary>
        /// <param name="viewModel">View model for constructor</param>
        public CalendarView(CalendarViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
