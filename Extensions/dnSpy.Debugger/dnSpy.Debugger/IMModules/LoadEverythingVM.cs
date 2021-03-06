﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using dnlib.DotNet;
using dnlib.Threading;
using dnSpy.Contracts.MVVM;

namespace dnSpy.Debugger.IMModules {
	sealed class MyCancellationToken : ICancellationToken {
		/*readonly*/ CancellationToken cancellationToken;

		public MyCancellationToken(CancellationToken cancellationToken) {
			this.cancellationToken = cancellationToken;
		}

		public void ThrowIfCancellationRequested() => cancellationToken.ThrowIfCancellationRequested();
	}

	sealed class LoadEverythingVM : ViewModelBase, IDisposable {
		public ICommand CancelCommand => new RelayCommand(a => Cancel(), a => CanCancel);

		readonly ModuleDef[] modules;
		readonly CancellationTokenSource cancellationTokenSource;
		readonly CancellationToken cancellationToken;

		public LoadEverythingVM(IEnumerable<ModuleDef> modules) {
			this.modules = modules.ToArray();
			this.cancellationTokenSource = new CancellationTokenSource();
			this.cancellationToken = cancellationTokenSource.Token;

			// Make sure the name is shown. Should be removed when the user can cancel since then
			// the UI will update automatically instead of being frozen.
			if (this.modules.Length != 0)
				CurrentItemName = CalculateCurrentItemName(this.modules[0]);
		}

		public bool CanCancel => !cancelling;
		bool cancelling;

		public void Cancel() {
			if (cancelling)
				return;
			cancelling = true;
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}

		public void Dispose() => Cancel();

		public bool WasCanceled {
			get { return wasCanceled; }
			set {
				if (wasCanceled != value) {
					wasCanceled = value;
					OnPropertyChanged(nameof(WasCanceled));
				}
			}
		}
		bool wasCanceled;

		public bool HasCompleted {
			get { return hasCompleted; }
			set {
				if (hasCompleted != value) {
					hasCompleted = value;
					OnPropertyChanged(nameof(HasCompleted));
				}
			}
		}
		bool hasCompleted;

		public string CurrentItemName {
			get { return currentItemName; }
			set {
				if (currentItemName != value) {
					currentItemName = value;
					OnPropertyChanged(nameof(CurrentItemName));
				}
			}
		}
		string currentItemName;

		public void LoadFiles() {
			foreach (var module in modules) {
				try {
					CurrentItemName = CalculateCurrentItemName(module);
					module.LoadEverything(new MyCancellationToken(cancellationToken));
				}
				catch (OperationCanceledException) {
					WasCanceled = true;
					break;
				}
			}
			HasCompleted = true;
			OnCompleted?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler OnCompleted;

		string CalculateCurrentItemName(ModuleDef module) {
			var asm = module.Assembly;
			if (asm != null) {
				if (module.IsManifestModule)
					return asm.FullName;
				return string.Format("{0} ({1})", module.Name, asm.FullName);
			}
			return module.Name;
		}
	}
}
