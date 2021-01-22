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
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;

	public interface ICustomValidator { }

	public interface ICustomValidator<T, in TProperty> : ICustomValidator {
		void Configure(ICustomRuleBuilder<T, TProperty> rule);
	}

	internal class CustomValidator<T, TProperty> : ICustomValidator<T, TProperty> {
		private Action<IPropertyValidatorContext<T, TProperty>> _action;

		public CustomValidator(Action<IPropertyValidatorContext<T, TProperty>> action) {
			_action = action;
		}

		public void Configure(ICustomRuleBuilder<T, TProperty> rule) => rule.Custom(_action);
	}

	internal class AsyncCustomValidator<T, TProperty> : ICustomValidator<T, TProperty> {
		private Func<IPropertyValidatorContext<T, TProperty>, CancellationToken, Task> _action;

		public AsyncCustomValidator(Func<IPropertyValidatorContext<T, TProperty>, CancellationToken, Task> action) {
			_action = action;
		}

		public void Configure(ICustomRuleBuilder<T, TProperty> rule) => rule.Custom(action: null, asyncAction: _action);
	}


	public class PropertyValidatorOptions<T, TProperty> : IPropertyValidator, IRuleBuilderOptions<T,TProperty>, ICustomRuleBuilder<T,TProperty> {
		private string _errorMessage;
		private Func<IPropertyValidatorContext<T,TProperty>, string> _errorMessageFactory;
		private Func<IValidationContext, bool> _condition;
		private Func<IValidationContext, CancellationToken, Task<bool>> _asyncCondition;
		private string _errorCode;

		private protected Action<IPropertyValidatorContext<T,TProperty>> ValidationAction { get; set; }
		private protected Func<IPropertyValidatorContext<T,TProperty>, CancellationToken, Task> AsyncValidationAction { get; set; }

		/// <summary>
		/// Whether or not this validator has a condition associated with it.
		/// </summary>
		public bool HasCondition => _condition != null;

		/// <summary>
		/// Whether or not this validator has an async condition associated with it.
		/// </summary>
		public bool HasAsyncCondition => _asyncCondition != null;

		/// <summary>
		/// Performs validation
		/// </summary>
		/// <param name="context"></param>
		internal void Validate(IPropertyValidatorContext<T,TProperty> context) {
			ValidationAction(context);
		}

		/// <summary>
		/// Performs validation asynchronously.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="cancellation"></param>
		internal Task ValidateAsync(IPropertyValidatorContext<T,TProperty> context, CancellationToken cancellation) {
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
			if (HasAsyncCondition) return true;

			// If there's an async action and no sync action, always run the async version
			if (AsyncValidationAction != null && ValidationAction == null) return true;

			// If both sync & async actions have been provided, prefer the async action
			// if ValidateAsync has been called on the root validator.
			if (AsyncValidationAction != null && ValidationAction != null) {
				return context.IsAsync();
			}

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
				// _defaultErrorCode ??= value;
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
		public string GetErrorMessage(IPropertyValidatorContext<T,TProperty> context) {
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
		public void SetErrorMessage(Func<IPropertyValidatorContext<T,TProperty>, string> errorFactory) {
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

		internal Action<T, IPropertyValidatorContext<T,TProperty>, string> OnFailure { get; set; }

		internal IExecutableValidationRule<T> ParentRule { get; set; }

		IRuleBuilderOptions<T, TProperty> IRuleBuilderOptions<T, TProperty>.DependentRules(Action action) {
			var dependencyContainer = new List<IExecutableValidationRule<T>>();

			// Capture any rules added to the parent validator inside this delegate.
			using (ParentRule.ParentValidator.Rules.Capture(dependencyContainer.Add)) {
				action();
			}

			if (ParentRule.RuleSets != null && ParentRule.RuleSets.Length > 0) {
				foreach (var dependentRule in dependencyContainer) {
					if (dependentRule is PropertyRule<T, TProperty> propRule && propRule.RuleSets == null) {
						propRule.RuleSets = ParentRule.RuleSets;
					}
				}
			}

			ParentRule.AddDependentRules(dependencyContainer);
			return this;

		}

		IRuleBuilderOptions<T, TProperty> IRuleBuilder<T, TProperty>.SetValidator(ICustomValidator<T, TProperty> validator) {
			return ((IRuleBuilder<T, TProperty>) ParentRule).SetValidator(validator);
		}

		IRuleBuilderOptions<T, TProperty> IRuleBuilder<T, TProperty>.SetValidator(IValidator<TProperty> validator, params string[] ruleSets) {
			return ((IRuleBuilder<T, TProperty>) ParentRule).SetValidator(validator, ruleSets);
		}

		IRuleBuilderOptions<T, TProperty> IRuleBuilder<T, TProperty>.SetValidator<TValidator>(Func<T, TValidator> validatorProvider, params string[] ruleSets) {
			return ((IRuleBuilder<T, TProperty>) ParentRule).SetValidator(validatorProvider, ruleSets);
		}

		IRuleBuilderOptions<T, TProperty> IRuleBuilder<T, TProperty>.SetValidator<TValidator>(Func<T, TProperty, TValidator> validatorProvider, params string[] ruleSets) {
			return ((IRuleBuilder<T, TProperty>) ParentRule).SetValidator(validatorProvider, ruleSets);
		}

		IRuleBuilderOptions<T, TProperty> ICustomRuleBuilder<T, TProperty>.Custom(Action<IPropertyValidatorContext<T, TProperty>> action, Func<IPropertyValidatorContext<T, TProperty>, CancellationToken, Task> asyncAction) {
			ValidationAction = action;
			AsyncValidationAction = asyncAction;
			return this;
		}
	}
}
