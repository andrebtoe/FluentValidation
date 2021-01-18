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
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;


	public abstract class PropertyValidator<T, TProperty> : CustomValidator<T, TProperty> {

		public PropertyValidator() {
			ValidationAction = context => {
				if (!IsValid(context)) {
					context.AddFailure();
				}
			};
		}

		protected abstract bool IsValid(PropertyValidatorContext<T, TProperty> context);
	}


	public abstract class AsyncPropertyValidator<T, TProperty> : CustomValidator<T, TProperty> {

		public AsyncPropertyValidator() {
			AsyncValidationAction = async (context, cancel) => {
				if (!await IsValidAsync(context, cancel)) {
					context.AddFailure();
				}
			};
		}

		protected abstract Task<bool> IsValidAsync(PropertyValidatorContext<T, TProperty> context, CancellationToken cancellation);
	}


	public class CustomValidator<T, TProperty> : IPropertyValidator {
		private string _errorMessage;
		private Func<PropertyValidatorContext<T,TProperty>, string> _errorMessageFactory;
		private Func<IValidationContext, bool> _condition;
		private Func<IValidationContext, CancellationToken, Task<bool>> _asyncCondition;
		private string _errorCode;

		internal Action<PropertyValidatorContext<T,TProperty>> ValidationAction { get; init; }
		internal Func<PropertyValidatorContext<T,TProperty>, CancellationToken, Task> AsyncValidationAction { get; init; }

		/// <summary>
		/// Whether or not this validator has a condition associated with it.
		/// </summary>
		public bool HasCondition => _condition != null;

		/// <summary>
		/// Whether or not this validator has an async condition associated with it.
		/// </summary>
		public bool HasAsyncCondition => _asyncCondition != null;

		public virtual string Name => "CustomValidator";

		///// <inheritdoc />
		// public string Name {
		// 	get {
		// 		if (_originalErrorCode == null) {
		// 			_originalErrorCode = ValidatorOptions.Global.ErrorCodeResolver(this);
		// 		}
		// 		return _originalErrorCode;
		// 	}
		// }

		/// <summary>
		/// Retrieves a localized string from the LanguageManager.
		/// If an ErrorCode is defined for this validator, the error code is used as the key.
		/// If no ErrorCode is defined (or the language manager doesn't have a translation for the error code)
		/// then the fallback key is used instead.
		/// </summary>
		/// <param name="fallbackKey">The fallback key to use for translation, if no ErrorCode is available.</param>
		/// <returns>The translated error message template.</returns>
		protected string Localized(string fallbackKey) {
			var errorCode = ErrorCode;

			if (errorCode != null) {
				string result = ValidatorOptions.Global.LanguageManager.GetString(errorCode);

				if (!string.IsNullOrEmpty(result)) {
					return result;
				}
			}

			return ValidatorOptions.Global.LanguageManager.GetString(fallbackKey);
		}


		/// <summary>
		/// Performs validation
		/// </summary>
		/// <param name="context"></param>
		public void Validate(PropertyValidatorContext<T,TProperty> context) {
			ValidationAction(context);
		}

		/// <summary>
		/// Performs validation asynchronously.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="cancellation"></param>
		public Task ValidateAsync(PropertyValidatorContext<T,TProperty> context, CancellationToken cancellation) {
			if (AsyncValidationAction != null) {
				return AsyncValidationAction(context, cancellation);
			}
			else {
				ValidationAction(context);
				return Task.CompletedTask;
			}
		}

		/// <inheritdoc />
		public virtual bool ShouldValidateAsynchronously(IValidationContext context) {
			// If the user has applied an async condition, then always go through the async path
			// even if validator is being run synchronously.
			if (HasAsyncCondition || AsyncValidationAction != null) return true;
			return false;
		}

		/// <summary>
		/// Adds a condition for this validator. If there's already a condition, they're combined together with an AND.
		/// </summary>
		/// <param name="condition"></param>
		public void ApplyCondition(Func<IValidationContext, bool> condition) {
			if (_condition == null) {
				_condition = condition;
			}
			else {
				var original = _condition;
				_condition = ctx => condition(ctx) && original(ctx);
			}
		}

		/// <summary>
		/// Adds a condition for this validator. If there's already a condition, they're combined together with an AND.
		/// </summary>
		/// <param name="condition"></param>
		public void ApplyAsyncCondition(Func<IValidationContext, CancellationToken, Task<bool>> condition) {
			if (_asyncCondition == null) {
				_asyncCondition = condition;
			}
			else {
				var original = _asyncCondition;
				_asyncCondition = async (ctx, ct) => await condition(ctx, ct) && await original(ctx, ct);
			}
		}

		internal bool InvokeCondition(IValidationContext context) {
			if (_condition != null) {
				return _condition(context);
			}

			return true;
		}

		internal async Task<bool> InvokeAsyncCondition(IValidationContext context, CancellationToken token) {
			if (_asyncCondition != null) {
				return await _asyncCondition(context, token);
			}

			return true;
		}

		/// <summary>
		/// Function used to retrieve custom state for the validator
		/// </summary>
		public Func<PropertyValidatorContext<T,TProperty>, object> CustomStateProvider { get; set; }

		/// <summary>
		/// Function used to retrieve the severity for the validator
		/// </summary>
		public Func<PropertyValidatorContext<T,TProperty>, Severity> SeverityProvider { get; set; }

		/// <summary>
		/// Retrieves the error code.
		/// </summary>
		public string ErrorCode {
			get => _errorCode;
			set {
				_errorCode = value;
			}
		}

		/// <summary>
		/// Returns the default error message template for this validator, when not overridden.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetDefaultMessageTemplate() => "No default error message has been specified";

		/// <summary>
		/// Gets the error message. If a context is supplied, it will be used to format the message if it has placeholders.
		/// If no context is supplied, the raw unformatted message will be returned, containing placeholders.
		/// </summary>
		/// <param name="context">The current property validator context.</param>
		/// <returns>Either the formatted or unformatted error message.</returns>
		public string GetErrorMessage(PropertyValidatorContext<T,TProperty> context) {
			string rawTemplate = _errorMessageFactory?.Invoke(context) ?? _errorMessage ?? GetDefaultMessageTemplate();

			if (context == null) {
				return rawTemplate;
			}

			return context.MessageFormatter.BuildMessage(rawTemplate);
		}

		/// <summary>
		/// Gets the raw unformatted error message. Placeholders will not have been rewritten.
		/// </summary>
		/// <returns></returns>
		public string GetUnformattedErrorMessage() {
			return _errorMessageFactory?.Invoke(null) ?? _errorMessage ?? GetDefaultMessageTemplate();
		}

		/// <summary>
		/// Sets the overridden error message template for this validator.
		/// </summary>
		/// <param name="errorFactory">A function for retrieving the error message template.</param>
		public void SetErrorMessage(Func<PropertyValidatorContext<T,TProperty>, string> errorFactory) {
			_errorMessageFactory = errorFactory;
			_errorMessage = null;
		}

		/// <summary>
		/// Sets the overridden error message template for this validator.
		/// </summary>
		/// <param name="errorMessage">The error message to set</param>
		public void SetErrorMessage(string errorMessage) {
			_errorMessage = errorMessage;
			_errorMessageFactory = null;
		}

		internal Action<T, PropertyValidatorContext<T,TProperty>, string> OnFailure { get; set; }
	}
}
