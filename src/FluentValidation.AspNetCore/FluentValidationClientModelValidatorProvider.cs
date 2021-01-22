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
namespace FluentValidation.AspNetCore {
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations;
	using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
	using System.Linq;
	using System.Reflection;
	using FluentValidation.Internal;
	using FluentValidation.Validators;
	using Microsoft.AspNetCore.Http;
	using Microsoft.AspNetCore.Mvc.DataAnnotations;

	public delegate IClientModelValidator FluentValidationClientValidatorFactory(ClientValidatorProviderContext context, IValidationRule rule, ICustomValidator validator, IPropertyValidator options);

	/// <summary>
	/// Used to generate clientside metadata from FluentValidation's rules.
	/// </summary>
	public class FluentValidationClientModelValidatorProvider : IClientModelValidatorProvider{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ValidatorDescriptorCache _descriptorCache = new ValidatorDescriptorCache();

		public Dictionary<Type, FluentValidationClientValidatorFactory> ClientValidatorFactories { get; } = new() {
			{ typeof(INotNullValidator), (context, rule, validator, options) => new RequiredClientValidator(rule, validator, options) },
			{ typeof(INotEmptyValidator), (context, rule, validator, options) => new RequiredClientValidator(rule, validator, options) },
			{ typeof(IEmailValidator), (context, rule, validator, options) => new EmailClientValidator(rule, validator, options) },
			{ typeof(IRegularExpressionValidator), (context, rule, validator, options) => new RegexClientValidator(rule, validator, options) },
			{ typeof(IMaximumLengthValidator), (context, rule, validator, options) => new MaxLengthClientValidator(rule, validator, options) },
			{ typeof(IMaximumLengthValidator), (context, rule, validator, options) => new MinLengthClientValidator(rule, validator, options) },
			{ typeof(IExactLengthValidator), (context, rule, validator, options) => new StringLengthClientValidator(rule, validator, options)},
			{ typeof(ILengthValidator), (context, rule, validator, options) => new StringLengthClientValidator(rule, validator, options)},
			{ typeof(IInclusiveBetweenValidator), (context, rule, validator, options) => new RangeClientValidator(rule, validator, options) },
			{ typeof(IGreaterThanOrEqualValidator), (context, rule, validator, options) => new RangeMinClientValidator(rule, validator, options) },
			{ typeof(ILessThanOrEqualValidator), (context, rule, validator, options) => new RangeMaxClientValidator(rule, validator, options) },
			{ typeof(IEqualValidator), (context, rule, validator, options) => new EqualToClientValidator(rule, validator, options) },
			{ typeof(ICreditCardValidator), (context, rule, validator, options) => new CreditCardClientValidator(rule, validator, options) },
		};

		public FluentValidationClientModelValidatorProvider(IHttpContextAccessor httpContextAccessor) {
			_httpContextAccessor = httpContextAccessor;
		}

		public void Add(Type validatorType, FluentValidationClientValidatorFactory factory) {
			if (validatorType == null) throw new ArgumentNullException(nameof(validatorType));
			if (factory == null) throw new ArgumentNullException(nameof(factory));

			ClientValidatorFactories[validatorType] = factory;
		}

		public void CreateValidators(ClientValidatorProviderContext context) {
			var descriptor = _descriptorCache.GetCachedDescriptor(context, _httpContextAccessor);

			if (descriptor != null) {
				var propertyName = context.ModelMetadata.PropertyName;

				var validatorsWithRules = from rule in descriptor.GetRulesForMember(propertyName)
					where !rule.HasCondition && !rule.HasAsyncCondition
					let validators = rule.Validators
					where validators.Any()
					from propertyValidator in validators
					where !propertyValidator.Options.HasCondition && !propertyValidator.Options.HasAsyncCondition
					let modelValidatorForProperty = GetModelValidator(context, rule, propertyValidator.CustomValidator, propertyValidator.Options)
					where modelValidatorForProperty != null
					select modelValidatorForProperty;

				var list = validatorsWithRules.ToList();

				foreach (var propVal in list) {
					context.Results.Add(new ClientValidatorItem {
						Validator = propVal,
						IsReusable = false
					});
				}

				// Must ensure there is at least 1 ClientValidatorItem, set to IsReusable = false
				// otherwise MVC will cache the list of validators, assuming there will always be 0 validators for that property
				// Which isn't true - we may be using the RulesetForClientsideMessages attribute (or some other mechanism) that can change the client validators that are available
				// depending on some context.
				if (list.Count == 0) {
					context.Results.Add(new ClientValidatorItem {IsReusable = false});
				}

				HandleNonNullableValueTypeRequiredRule(context);
			}
		}

		// If the property is a non-nullable value type, then MVC will have already generated a Required rule.
		// If we've provided our own Requried rule, then remove the MVC one.
		protected virtual void HandleNonNullableValueTypeRequiredRule(ClientValidatorProviderContext context) {
			bool isNonNullableValueType = !TypeAllowsNullValue(context.ModelMetadata.ModelType);

			if (isNonNullableValueType) {
				bool fvHasRequiredRule = context.Results.Any(x => x.Validator is RequiredClientValidator);

				if (fvHasRequiredRule) {
					var dataAnnotationsRequiredRule = context.Results
						.FirstOrDefault(x => x.Validator is RequiredAttributeAdapter);
					context.Results.Remove(dataAnnotationsRequiredRule);
				}
			}
		}

		protected virtual IClientModelValidator GetModelValidator(ClientValidatorProviderContext context, IValidationRule rule, ICustomValidator customValidator, IPropertyValidator options)	{
			var type = customValidator.GetType();

			var factory = ClientValidatorFactories
				.Where(x => x.Key.IsAssignableFrom(type))
				.Select(x => x.Value)
				.FirstOrDefault();

			if (factory != null) {
				var ruleSetToGenerateClientSideRules = RuleSetForClientSideMessagesAttribute.GetRuleSetsForClientValidation(_httpContextAccessor?.HttpContext);
				bool executeDefaultRule = ruleSetToGenerateClientSideRules.Contains(RulesetValidatorSelector.DefaultRuleSetName, StringComparer.OrdinalIgnoreCase)
          && (rule.RuleSets.Length == 0 || rule.RuleSets.Contains(RulesetValidatorSelector.DefaultRuleSetName, StringComparer.OrdinalIgnoreCase));

				bool shouldExecute = ruleSetToGenerateClientSideRules.Intersect(rule.RuleSets, StringComparer.OrdinalIgnoreCase).Any() || executeDefaultRule;

				if (shouldExecute) {
					return factory.Invoke(context, rule, customValidator, options);
				}
			}

			return null;
		}

		private bool TypeAllowsNullValue(Type type) {
			return (!type.IsValueType || Nullable.GetUnderlyingType(type) != null);
		}
	}

}
