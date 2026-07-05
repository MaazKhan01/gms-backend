using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Guest
{
    public class DeleteMultipleGuests
    {
        public List<Guid> SelectedGuestsToDelete { get; set; } = [];
    }
}
