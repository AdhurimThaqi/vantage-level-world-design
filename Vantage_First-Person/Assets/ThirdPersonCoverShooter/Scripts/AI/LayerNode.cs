using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CoverShooter.AI
{
    [Serializable]
    public class LayerNode : Editable, ISerializationCallbackReceiver
    {
        public object Attachment { get { return _attachment; } }
        public FieldInfo[] Fields { get { return _fields; } }

        /// <summary>
        /// Parent node ID.
        /// </summary>
        public int Parent;

        /// <summary>
        /// Position of the node inside the editor.
        /// </summary>
        [HideInInspector]
        public Vector2 EditorPosition;

        /// <summary>
        /// Width of the node drawn inside the editor.
        /// </summary>
        [HideInInspector]
        [NonSerialized]
        public float EditorWidth;

        /// <summary>
        /// Height of the node drawn inside the editor.
        /// </summary>
        [HideInInspector]
        [NonSerialized]
        public float EditorHeight;

        [HideInInspector]
        [NonSerialized]
        public bool HasEditorTopConnection = false;

        [HideInInspector]
        [NonSerialized]
        public bool HasEditorLeftConnection = false;

        [HideInInspector]
        [NonSerialized]
        public bool HasEditorRightConnection = false;

        [HideInInspector]
        [NonSerialized]
        public bool HasEditorBottomConnection = false;

        [NonSerialized]
        private object _attachment;

        [NonSerialized]
        private FieldInfo[] _fields;

        [SerializeField]
        private string _type;

        [SerializeField]
        private string[] _fieldNames;

        [SerializeField]
        private Value[] _serializedValues;

        [SerializeField]
        private Value[] _serializedValueArray;

        [SerializeField]
        private VariableReference[] _serializedVariableReferences;

        [SerializeField]
        private TriggerReference[] _serializedTriggerReferences;

        [SerializeField]
        private AIEvent[] _serializedEvents;

        public LayerNode()
            : base()
        {
            EditorWidth = 100;
            EditorHeight = 100;
        }

        public LayerNode(object attachment)
            : this()
        {
            _attachment = attachment;

            if (attachment != null)
                _fields = attachment.GetType().GetFields();
        }

        public void SetValue(int index, Value value)
        {
            var cursor = 0;

            for (int i = 0; i < Fields.Length; i++)
            {
                var field = Fields[i];

                if (field.FieldType == typeof(Value))
                {
                    if (cursor == index)
                    {
                        field.SetValue(Attachment, value);
                        break;
                    }
                    else
                        cursor++;
                }
                else if (field.FieldType == typeof(Value[]))
                {
                    var array = (Value[])field.GetValue(Attachment);

                    if (index - cursor < array.Length)
                    {
                        array[index - cursor] = value;
                        field.SetValue(Attachment, array);
                        break;
                    }
                    else
                        cursor += array.Length;
                }
            }
        }

        public Value GetValue(int index)
        {
            var cursor = 0;

            for (int i = 0; i < Fields.Length; i++)
            {
                var field = Fields[i];

                if (field.FieldType == typeof(Value))
                {
                    if (cursor == index)
                        return (Value)field.GetValue(Attachment);
                    else
                        cursor++;
                }
                else if (field.FieldType == typeof(Value[]))
                {
                    var array = (Value[])field.GetValue(Attachment);

                    if (index - cursor < array.Length)
                        return array[index - cursor];
                    else
                        cursor += array.Length;
                }
            }

            return new Value();
        }

        /// <summary>
        /// Returns true if the given variable id is referenced anywhere in the node.
        /// </summary>
        public virtual bool IsUsingVariable(int id)
        {
            if (_fields == null || _attachment == null)
                return false;

            for (int i = 0; i < _fields.Length; i++)
                if (_fields[i].FieldType == typeof(Value))
                {
                    var value = (Value)_fields[i].GetValue(_attachment);

                    if (value.ID == id)
                        return true;
                }
                else if (_fields[i].FieldType == typeof(VariableReference))
                {
                    var reference = (VariableReference)_fields[i].GetValue(_attachment);

                    if (reference.ID == id)
                        return true;
                }

            return false;
        }

        /// <summary>
        /// Clears value references to the certain id.
        /// </summary>
        public virtual void ClearValue(int id)
        {
            if (Attachment == null || _fields == null)
                return;

            for (int i = 0; i < _fields.Length; i++)
                if (_fields[i].FieldType == typeof(Value))
                {
                    var value = (Value)_fields[i].GetValue(Attachment);

                    if (value.ID == id)
                    {
                        value.ID = 0;
                        _fields[i].SetValue(Attachment, value);
                    }

                    if (value.Array != null)
                        for (int j = 0; j < value.Array.Length; j++)
                            if (value.Array[j].ID == id)
                                value.Array[j].ID = 0;
                }
                else if (_fields[i].FieldType == typeof(Value[]))
                {
                    var values = (Value[])_fields[i].GetValue(Attachment);

                    if (values != null)
                        for (int j = 0; j < values.Length; j++)
                        {
                            if (values[j].ID == id)
                                values[j].ID = 0;

                            if (values[j].Array != null)
                                for (int k = 0; k < values[j].Array.Length; k++)
                                    if (values[j].Array[k].ID == id)
                                        values[j].Array[k].ID = 0;
                        }
                }
                else if (_fields[i].FieldType == typeof(VariableReference))
                {
                    var reference = (VariableReference)_fields[i].GetValue(Attachment);

                    if (reference.ID == id)
                    {
                        reference.ID = 0;
                        _fields[i].SetValue(Attachment, reference);
                    }
                }
                else if (_fields[i].FieldType == typeof(TriggerReference))
                {
                    var reference = (TriggerReference)_fields[i].GetValue(Attachment);

                    if (reference.ID == id)
                    {
                        reference.ID = 0;
                        _fields[i].SetValue(Attachment, reference);
                    }
                }
        }

        /// <summary>
        /// Serialize the attachment and values.
        /// </summary>
        public virtual void OnBeforeSerialize()
        {
            if (_attachment == null)
            {
                _type = null;
                _fieldNames = null;
                _serializedValues = null;
                _serializedVariableReferences = null;
                _serializedVariableReferences = null;
                return;
            }

            _type = _attachment.GetType().AssemblyQualifiedName;

            if (_fieldNames == null || _fieldNames.Length != _fields.Length) _fieldNames = new string[_fields.Length];
            if (_serializedValues == null || _serializedValues.Length != _fields.Length) _serializedValues = new Value[_fields.Length];
            if (_serializedVariableReferences == null || _serializedVariableReferences.Length != _fields.Length) _serializedVariableReferences = new VariableReference[_fields.Length];
            if (_serializedTriggerReferences == null || _serializedTriggerReferences.Length != _fields.Length) _serializedTriggerReferences = new TriggerReference[_fields.Length];
            if (_serializedEvents == null || _serializedEvents.Length != _fields.Length) _serializedEvents = new AIEvent[_fields.Length];
            _serializedValueArray = null;

            for (int i = 0; i < _fields.Length; i++)
            {
                _fieldNames[i] = _fields[i].Name;

                if (_fields[i].FieldType == typeof(Value))
                {
                    var value = (Value)_fields[i].GetValue(_attachment);
                    _serializedValues[i] = sanitizeValue(value);
                }
                else if (_fields[i].FieldType == typeof(Value[]))
                {
                    var array = (Value[])_fields[i].GetValue(_attachment);
                    _serializedValueArray = sanitizeValues(array);
                }
                else if (_fields[i].FieldType == typeof(VariableReference))
                    _serializedVariableReferences[i] = (VariableReference)_fields[i].GetValue(_attachment);
                else if (_fields[i].FieldType == typeof(TriggerReference))
                    _serializedTriggerReferences[i] = (TriggerReference)_fields[i].GetValue(_attachment);
                else if (_fields[i].FieldType == typeof(AIEvent))
                    _serializedEvents[i] = (AIEvent)_fields[i].GetValue(_attachment);
            }
        }

        /// <summary>
        /// Deserialize the attachment and values.
        /// </summary>
        public virtual void OnAfterDeserialize()
        {
            if (_type == null || _type.Length == 0)
                return;

            var type = Type.GetType(_type);

            if (type == null)
                return;

            _attachment = Activator.CreateInstance(type);
            _fields = type.GetFields();

            if (_fieldNames != null)
                for (int i = 0; i < _fields.Length; i++)
                    for (int j = 0; j < _fieldNames.Length; j++)
                        if (_fieldNames[j] == _fields[i].Name)
                        {
                            if (_fields[i].FieldType == typeof(Value))
                            {
                                if (_serializedValues != null && _serializedValues.Length > j)
                                    _fields[i].SetValue(_attachment, sanitizeValue(_serializedValues[j]));
                            }
                            else if (_fields[i].FieldType == typeof(Value[]))
                            {
                                _fields[i].SetValue(_attachment, sanitizeValues(_serializedValueArray));
                            }
                            else if (_fields[i].FieldType == typeof(VariableReference))
                            {
                                if (_serializedVariableReferences != null && _serializedVariableReferences.Length > j)
                                    _fields[i].SetValue(_attachment, _serializedVariableReferences[j]);
                            }
                            else if (_fields[i].FieldType == typeof(TriggerReference))
                            {
                                if (_serializedTriggerReferences != null && _serializedTriggerReferences.Length > j)
                                    _fields[i].SetValue(_attachment, _serializedTriggerReferences[j]);
                            }
                            else if (_fields[i].FieldType == typeof(AIEvent))
                            {
                                if (_serializedEvents != null && _serializedEvents.Length > j)
                                    _fields[i].SetValue(_attachment, _serializedEvents[j]);
                            }

                            break;
                        }
        }

        private const int _maxSerializedArrayDepth = 8;

        private static Value sanitizeValue(Value value)
        {
            return sanitizeValue(value, 0, new HashSet<Value[]>());
        }

        private static Value sanitizeValue(Value value, int depth, HashSet<Value[]> visited)
        {
            if (value.Type != ValueType.Array || value.Array == null)
                return value;

            if (depth >= _maxSerializedArrayDepth)
            {
                value.Array = null;
                value.Count = 0;
                return value;
            }

            var original = value.Array;

            if (!visited.Add(original))
            {
                value.Array = null;
                value.Count = 0;
                return value;
            }

            var clone = new Value[original.Length];

            for (int i = 0; i < original.Length; i++)
                clone[i] = sanitizeValue(original[i], depth + 1, visited);

            visited.Remove(original);

            value.Array = clone;
            return value;
        }

        private static Value[] sanitizeValues(Value[] values)
        {
            return sanitizeValues(values, 0, new HashSet<Value[]>());
        }

        private static Value[] sanitizeValues(Value[] values, int depth, HashSet<Value[]> visited)
        {
            if (values == null)
                return null;

            if (!visited.Add(values))
                return null;

            var clone = new Value[values.Length];

            for (int i = 0; i < values.Length; i++)
                clone[i] = sanitizeValue(values[i], depth + 1, visited);

            visited.Remove(values);

            return clone;
        }
    }
}
