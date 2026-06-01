using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.ViewModel.Common
{
	public class SendEmailResponse<T>
	{
		public bool SendSuccessfully { get; set; }
		public T Data { get; set; }
	}
}
