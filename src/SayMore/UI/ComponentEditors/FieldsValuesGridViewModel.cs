using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SayMore.Model.Fields;
using SayMore.Model.Files;
using SayMore.Model.Files.DataGathering;

namespace SayMore.UI.ComponentEditors
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class FieldsValuesGridViewModel
	{
		private ComponentFile _file;

		public Action ComponentFileChanged;
		public List<KeyValuePair<FieldValue, bool>> RowData { get; private set; }

		private Dictionary<string, IEnumerable<string>> _autoCompleteLists = new Dictionary<string,IEnumerable<string>>();
		private readonly IMultiListDataProvider _autoCompleteProvider;

		/// ------------------------------------------------------------------------------------
		public FieldsValuesGridViewModel(ComponentFile file,
			IEnumerable<string> customFieldIdsToDisplay)
			: this(file, new List<FieldDefinition>(0), customFieldIdsToDisplay, null)
		{
		}

		/// ------------------------------------------------------------------------------------
		public FieldsValuesGridViewModel(ComponentFile file,
			IEnumerable<FieldDefinition> defaultFieldIdsToDisplay, IEnumerable<string> customFieldIdsToDisplay,
			IMultiListDataProvider autoCompleteProvider)
		{
			if (autoCompleteProvider != null)
			{
				_autoCompleteProvider = autoCompleteProvider;
				_autoCompleteProvider.NewDataAvailable += HandleNewAutoCompleteDataAvailable;
				_autoCompleteLists = _autoCompleteProvider.GetValueLists();
			}

			SetComponentFile(file, defaultFieldIdsToDisplay, customFieldIdsToDisplay);
		}

		/// ------------------------------------------------------------------------------------
		public void SetComponentFile(ComponentFile file, IEnumerable<string> customFieldIdsToDisplay)
		{
			SetComponentFile(file, new List<FieldDefinition>(0), customFieldIdsToDisplay);
		}

		/// ------------------------------------------------------------------------------------
		public void SetComponentFile(ComponentFile file,
			IEnumerable<FieldDefinition> defaultFieldIdsToDisplay, IEnumerable<string> customFieldIdsToDisplay)
		{
			_file = file;

			RowData = new List<KeyValuePair<FieldValue, bool>>();
			LoadFields(defaultFieldIdsToDisplay);
			//todo: this separate call could go away once we just have one list (with the elements describing themselves via FieldDefintion)
			LoadFields(customFieldIdsToDisplay.Select(x=>new FieldDefinition(x,"string",new string[]{}){IsCustom=true}));

			if (ComponentFileChanged != null)
				ComponentFileChanged();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Load the field values into the model's data cache.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void LoadFields(IEnumerable<FieldDefinition> fieldsToDisplay)
		{
			foreach (var field in fieldsToDisplay)
			{
				if(!field.ShowInPropertiesGrid )
					continue;

				var fieldValue = new FieldValue(field.Key, _file.GetStringValue(field.Key, string.Empty));

				//TODO: make use of field.ReadOnly

				// Each row in the cache is a key/value pair. The key is the FieldValue object
				// and the value is a boolean indicating whether or not the field is custom.
				RowData.Add(new KeyValuePair<FieldValue, bool>(fieldValue.CreateCopy(), field.IsCustom));
			}
		}

		/// ------------------------------------------------------------------------------------
		public string GridSettingsName
		{
			get { return _file.FileType.FieldsGridSettingsName; }
		}

		/// ------------------------------------------------------------------------------------
		void HandleNewAutoCompleteDataAvailable(object sender, EventArgs e)
		{
			_autoCompleteLists = _autoCompleteProvider.GetValueLists();
		}

		/// ------------------------------------------------------------------------------------
		public AutoCompleteStringCollection GetAutoCompleteListForIndex(int index)
		{
			var fieldId = GetIdForIndex(index);
			var autoCompleteValues = new AutoCompleteStringCollection();

			if (!string.IsNullOrEmpty(fieldId))
			{
				IEnumerable<string> values;
				if (_autoCompleteLists.TryGetValue(fieldId, out values))
					autoCompleteValues.AddRange(values.ToArray());
			}

			return autoCompleteValues;
		}

		/// ------------------------------------------------------------------------------------
		public bool IsIndexForCustomField(int index)
		{
			return (index < RowData.Count ? RowData[index].Value : true);
		}

		/// ------------------------------------------------------------------------------------
		public bool CanDeleteRow(int index, out int indexToDelete)
		{
			indexToDelete = (index < RowData.Count ? index : -1);
			return IsIndexForCustomField(index);
		}

		/// ------------------------------------------------------------------------------------
		public FieldValue AddEmptyField()
		{
			var fieldValue = new FieldValue(string.Empty, string.Empty);
			RowData.Add(new KeyValuePair<FieldValue, bool>(fieldValue, true));
			return fieldValue;
		}

		/// ------------------------------------------------------------------------------------
		public string GetIdForIndex(int index)
		{
			return (index < RowData.Count ? RowData[index].Key.FieldId : null);
		}

		/// ------------------------------------------------------------------------------------
		public string GetValueForIndex(int index)
		{
			return (index < RowData.Count ? RowData[index].Key.Value : null);
		}

		/// ------------------------------------------------------------------------------------
		public void SetIdForIndex(string id, int index)
		{
			var fieldValue = (index == RowData.Count ? AddEmptyField() : RowData[index].Key);
			fieldValue.FieldId = (id != null ? id.Trim() : string.Empty).Replace(' ', '_');
		}

		/// ------------------------------------------------------------------------------------
		public void SetValueForIndex(string value, int index)
		{
			var fieldValue = (index == RowData.Count ? AddEmptyField() : RowData[index].Key);
			fieldValue.Value = (value != null ? value.Trim() : string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		public void RemoveFieldForIndex(int index)
		{
			var field = RowData[index].Key;

			var origField = _file.MetaDataFieldValues.Find(x => x.FieldId == field.FieldId);
			if (origField != null)
				_file.MetaDataFieldValues.Remove(origField);

			_file.Save();
			RowData.RemoveAt(index);
		}

		/// ------------------------------------------------------------------------------------
		public void SaveFieldForIndex(int index)
		{
			var newField = RowData[index].Key;
			var oldField = _file.MetaDataFieldValues.Find(x => x.FieldId == newField.FieldId);

			// Don't bother doing anything if the old value is the same as the new value.
			if (oldField == newField)
				return;

			// TODO: handle case where new name is different.

			string failureMessage;
			_file.SetValue(newField, out failureMessage);

			if (failureMessage != null)
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(failureMessage);

			_file.Save();
		}
	}
}
