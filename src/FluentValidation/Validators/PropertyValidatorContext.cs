#region License
// Copyright (c) .NET Foundation and contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// The latest version of this file can be found at https://github.com/FluentValidation/FluentValidation
#endregion

namespace FluentValidation.Validators {
	using System;
	using System.Collections.Generic;
	using Internal;
	using Results;

	public interface IPropertyValidatorContext<out TProperty> {
		string PropertyName { get; }
		string DisplayName { get; }

		string RawPropertyName { get; }

		MessageFormatter MessageFormatter { get; }
		TProperty PropertyValue { get; }

		/// <summary>
		/// Adds a new validation failure.
		/// </summary>
		/// <param name="failure">The failure to add.</param>
		/// <exception cref="ArgumentNullException"></exception>
		void AddFailure(ValidationFailure failure);

		/// <summary>
		/// Adds a new validation failure.
		/// </summary>
		/// <param name="propertyName">The property name</param>
		/// <param name="errorMessage">The error message</param>
		void AddFailure(string propertyName, string errorMessage);

		/// <summary>
		/// Adds a new validation failure (the property name is inferred)
		/// </summary>
		/// <param name="errorMessage">The error message</param>
		void AddFailure(string errorMessage);

		void AddFailure();

	}

	public interface IPropertyValidatorContext<T, out TProperty> : IPropertyValidatorContext<TProperty> {
		ValidationContext<T> ParentContext { get; }

		T InstanceToValidate { get; }
	}


	public class PropertyValidatorContext<T, TProperty> : IPropertyValidatorContext<T,TProperty> {
		private TProperty _propertyValue;
		private Lazy<TProperty> _propertyValueAccessor;

		public ValidationContext<T> ParentContext { get; }

		internal IValidationRule<T, TProperty> Rule { get; }
		public string PropertyName { get; }

		public string DisplayName => Rule.GetDisplayName(ParentContext);

		public string RawPropertyName => Rule.PropertyName;

		public T InstanceToValidate => ParentContext.InstanceToValidate;
		public MessageFormatter MessageFormatter => ParentContext.MessageFormatter;

		internal ICustomValidator<T, TProperty> ValidatorInstance { get; private set; }
		internal PropertyValidatorOptions<T,TProperty> Options { get; private set; }

		//Lazily load the property value
		//to allow the delegating validator to cancel validation before value is obtained
		public TProperty PropertyValue
			=> _propertyValueAccessor != null ? _propertyValueAccessor.Value : _propertyValue;

		public PropertyValidatorContext(ValidationContext<T> parentContext, IValidationRule<T, TProperty> rule, string propertyName, TProperty propertyValue) {
			ParentContext = parentContext;
			Rule = rule;
			PropertyName = propertyName;
			_propertyValue = propertyValue;
		}

		public PropertyValidatorContext(ValidationContext<T> parentContext, IValidationRule<T, TProperty> rule, string propertyName, Lazy<TProperty> propertyValueAccessor) {
			ParentContext = parentContext;
			Rule = rule;
			PropertyName = propertyName;
			_propertyValueAccessor = propertyValueAccessor;
		}

		/// <summary>
		/// Adds a new validation failure.
		/// </summary>
		/// <param name="failure">The failure to add.</param>
		/// <exception cref="ArgumentNullException"></exception>
		public void AddFailure(ValidationFailure failure) {
			if (failure == null) throw new ArgumentNullException(nameof(failure), "A failure must be specified when calling AddFailure");
			PrepareMessageFormatter();
			ParentContext.Failures.Add(failure);
		}

		/// <summary>
		/// Adds a new validation failure.
		/// </summary>
		/// <param name="propertyName">The property name</param>
		/// <param name="errorMessage">The error message</param>
		public void AddFailure(string propertyName, string errorMessage) {
			errorMessage.Guard("An error message must be specified when calling AddFailure.", nameof(errorMessage));
			PrepareMessageFormatter();
			AddFailure(new ValidationFailure(propertyName ?? string.Empty, errorMessage));
		}

		/// <summary>
		/// Adds a new validation failure (the property name is inferred)
		/// </summary>
		/// <param name="errorMessage">The error message</param>
		public void AddFailure(string errorMessage) {
			errorMessage.Guard("An error message must be specified when calling AddFailure.", nameof(errorMessage));
			PrepareMessageFormatter();
			AddFailure(PropertyName, errorMessage);
		}

		public void AddFailure() {
			PrepareMessageFormatter();

			var error = Rule.MessageBuilder != null
				? Rule.MessageBuilder(new MessageBuilderContext<T,TProperty>(this, ValidatorInstance, Options))
				: Options.GetErrorMessage(this);

			var failure = new ValidationFailure(PropertyName, error, PropertyValue);
			failure.FormattedMessagePlaceholderValues = MessageFormatter.PlaceholderValues;
			failure.ErrorCode = ValidatorOptions.Global.ErrorCodeResolver(ValidatorInstance, Options);

			if (Options.CustomStateProvider != null) {
				failure.CustomState = Options.CustomStateProvider(this);
			}

			if (Options.SeverityProvider != null) {
				failure.Severity = Options.SeverityProvider(this);
			}

			if (Options.OnFailure != null) {
				Options.OnFailure(InstanceToValidate, this, failure.ErrorMessage);
			}

			ParentContext.Failures.Add(failure);
		}

		private void PrepareMessageFormatter() {
			MessageFormatter.AppendPropertyName(DisplayName);
			MessageFormatter.AppendPropertyValue(PropertyValue);

			// If there's a collection index cached in the root context data then add it
			// to the message formatter. This happens when a child validator is executed
			// as part of a call to RuleForEach. Usually parameters are not flowed through to
			// child validators, but we make an exception for collection indices.
			if (ParentContext.RootContextData.TryGetValue("__FV_CollectionIndex", out var index)) {
				// If our property validator has explicitly added a placeholder for the collection index
				// don't overwrite it with the cached version.
				if (!MessageFormatter.PlaceholderValues.ContainsKey("CollectionIndex")) {
					MessageFormatter.AppendArgument("CollectionIndex", index);
				}
			}
		}

		internal void Initialize(ICustomValidator<T,TProperty> validatorInstance, PropertyValidatorOptions<T,TProperty> options) {
			ParentContext.MessageFormatter.Reset();
			Options = options;
			ValidatorInstance = validatorInstance;
		}

	}
}
