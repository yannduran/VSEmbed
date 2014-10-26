﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Storage;

namespace VSThemeBrowser.VisualStudio {
	///<summary>Creates the MEF composition container used by the editor services.</summary>
	/// <remarks>Stolen, with much love and gratitude, from @JaredPar's EditorUtils.</remarks>
	public static class Mef {
		private static readonly string[] EditorComponents = {
			// Core editor components
			"Microsoft.VisualStudio.Platform.VSEditor",

			// Not entirely sure why this is suddenly needed
			"Microsoft.VisualStudio.Text.Internal",

			// Must include this because several editor options are actually stored as exported information 
			// on this DLL.  Including most importantly, the tabsize information
			"Microsoft.VisualStudio.Text.Logic",

			// Include this DLL to get several more EditorOptions including WordWrapStyle
			"Microsoft.VisualStudio.Text.UI",

			// Include this DLL to get more EditorOptions values and the core editor
			"Microsoft.VisualStudio.Text.UI.Wpf",

			// SLaks: Needed for VsUndoHistoryRegistry (which doesn't actually work), VsWpfKeyboardTrackingService, & probably others
			"Microsoft.VisualStudio.Editor.Implementation",

			// SLaks: Needed for IVsHierarchyItemManager, used by peek providers
			"Microsoft.VisualStudio.Shell.TreeNavigation.HierarchyProvider"
		};

		// I need to specify a full name to load from the GAC.
		// The version is added by my AssemblyResolve handler.
		const string FullNameSuffix = ", Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, processorArchitecture=MSIL";
		static IEnumerable<ComposablePartCatalog> GetCatalogs() {
			return EditorComponents.Select(c => new AssemblyCatalog(Assembly.Load(c + FullNameSuffix)));
		}
		public static readonly CompositionContainer Container =
			new CompositionContainer(new AggregateCatalog(GetCatalogs()));
		static Mef() {
			// Copied from Microsoft.VisualStudio.ComponentModelHost.ComponentModel.DefaultCompositionContainer
			Container.ComposeExportedValue<SVsServiceProvider>(
				new VsServiceProviderWrapper(ServiceProvider.GlobalProvider));

			Container.ComposeExportedValue<IDataStorageService>(
				new DataStorageService());

			// Needed because VsUndoHistoryRegistry tries to create IOleUndoManager from ILocalRegistry, which I presumably cannot do.
			Container.ComposeExportedValue((ITextUndoHistoryRegistry)
                Activator.CreateInstance(
					typeof(EditorUtils.EditorHost).Assembly
						.GetType("EditorUtils.Implementation.BasicUndo.BasicTextUndoHistoryRegistry"), true));
		}

		// Microsoft.VisualStudio.Editor.Implementation.DataStorage uses COM services
		// that read the user's color settings, which I cannot easily duplicate.
		class SimpleDataStorage : IDataStorage {
			public bool TryGetItemValue(string itemKey, out ResourceDictionary itemValue) {
				itemValue = new ResourceDictionary();
				var SetBackground = CreateSetter(itemValue, "Background");
				var SetForeground = CreateSetter(itemValue, "Foreground");

				// TODO: Use MEF-exported default formats; copy from MEFFontAndColorCategory
				switch (itemKey) {
					case "TextView Background":
						SetBackground(SystemColors.WindowColor);
						break;
					case "Plain Text":
						SetForeground(SystemColors.WindowTextColor);
						break;
					case "Selected Text":
						SetBackground(SystemColors.HighlightColor);
						SetForeground(SystemColors.HighlightTextColor);
						break;
					case "Inactive Selected Text":
						SetBackground(SystemColors.ControlColor);
						SetForeground(SystemColors.ControlTextColor);
						break;
					default:
						Debug.WriteLine("Returning unknown color key " + itemKey);
						SetBackground(Colors.Beige);
						SetForeground(Colors.MidnightBlue);
						break;
				}

				return true;
			}

			static Action<Color> CreateSetter(ResourceDictionary dict, string prefix) {
				return c => {
					dict[prefix] = new SolidColorBrush(c);
					dict[prefix + "Color"] = c;
				};
			}
		}
		[Export(typeof(IDataStorageService))]
		internal sealed class DataStorageService : IDataStorageService {
			readonly IDataStorage instance = new SimpleDataStorage();
			public IDataStorage GetDataStorage(string storageKey) { return instance; }
		}
	}
}
