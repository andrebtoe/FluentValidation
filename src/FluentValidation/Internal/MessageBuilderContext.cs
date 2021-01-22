﻿namespace FluentValidation.Internal {
	using System;
	using Resources;
	using Validators;

	public class MessageBuilderContext<T,TProperty> {
		private PropertyValidatorContext<T,TProperty> _innerContext;

		public MessageBuilderContext(PropertyValidatorContext<T,TProperty> innerContext, ICustomValidator<T,TProperty> validator, PropertyValidatorOptions<T,TProperty> options) {
			_innerContext = innerContext;
			Options = options;
			PropertyValidator = validator;
		}

		public PropertyValidatorOptions<T,TProperty> Options { get; }

		public ICustomValidator<T,TProperty> PropertyValidator { get; }

		public ValidationContext<T> ParentContext => _innerContext.ParentContext;

		public IValidationRule<T> Rule => _innerContext.Rule;

		public string PropertyName => _innerContext.PropertyName;

		public string DisplayName => _innerContext.DisplayName;

		public MessageFormatter MessageFormatter => _innerContext.MessageFormatter;

		public object InstanceToValidate => _innerContext.InstanceToValidate;
		public object PropertyValue => _innerContext.PropertyValue;

		public string GetDefaultMessage() {
			return Options.GetErrorMessage(_innerContext);
		}
		public static implicit operator PropertyValidatorContext<T,TProperty>(MessageBuilderContext<T,TProperty> ctx) {
			return ctx._innerContext;
		}
	}
}
