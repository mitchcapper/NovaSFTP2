using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NovaSFTP2.ViewModel {
	internal class OurCommand : ICommand {
		private readonly TimeSpan CommandMinRunTime = TimeSpan.FromSeconds(1);
		public bool CanExecute(object parameter) => !_running && enabled;
		public OurCommand(Func<Task> action, bool auto_disable = true) {
			async_action = action;
			this.auto_disable = auto_disable;
		}
		public OurCommand(Action action, bool auto_disable, bool in_background) {
			this.action = action;
			this.auto_disable = auto_disable;
			this.in_background = in_background;
		}
		private bool running {
			get { return _running; }
			set {
				if (_running == value)
					return;
				_running = value;
				CanExecuteChanged?.Invoke(this, null);
			}
		}
		private bool _running;
		public bool enabled {
			get { return _enabled; }
			set {
				if (_enabled == value)
					return;
				_enabled = value;
				CanExecuteChanged?.Invoke(this, null);
			}
		}
		private bool _enabled=true;
		public event EventHandler CanExecuteChanged;
		public Func<Task> async_action;
		public Action action;
		private readonly bool in_background;

		private readonly bool auto_disable;
		public async void Execute(object parameter) {
			if (!enabled)
				return;
			running = true;
			DateTime start_time = DateTime.MinValue;
			try {
				if (auto_disable)
					start_time = DateTime.Now;
				if (action != null) {
					if (in_background)
						await Task.Run(action);
					else
						action();
				} else
					await async_action.Invoke();
			} finally {
				if (auto_disable) {
					var diff = DateTime.Now - start_time;
					if (diff < CommandMinRunTime)
						await Task.Delay(CommandMinRunTime - diff);
				}
				running = false;
			}
		}
	}
}