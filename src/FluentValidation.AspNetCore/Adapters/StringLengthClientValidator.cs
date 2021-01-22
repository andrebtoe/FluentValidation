﻿#region License
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
	using Internal;
	using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
	using Resources;
	using Validators;

	internal class StringLengthClientValidator : ClientValidatorBase {
		public StringLengthClientValidator(IValidationRule rule, ICustomValidator validator, IPropertyValidator options) : base(rule, validator, options) {
		}

		public override void AddValidation(ClientModelValidationContext context) {
			var lengthVal = (ILengthValidator)Validator;

			MergeAttribute(context.Attributes, "data-val", "true");
			MergeAttribute(context.Attributes, "data-val-length", GetErrorMessage(lengthVal, context));
			MergeAttribute(context.Attributes, "data-val-length-max", lengthVal.Max.ToString());
			MergeAttribute(context.Attributes, "data-val-length-min", lengthVal.Min.ToString());
		}

		private string GetErrorMessage(ILengthValidator lengthVal, ClientModelValidationContext context) {
			var cfg = context.ActionContext.HttpContext.RequestServices.GetValidatorConfiguration();

			var formatter = cfg.MessageFormatterFactory()
				.AppendPropertyName(Rule.GetDisplayName(null))
				.AppendArgument("MinLength", lengthVal.Min)
				.AppendArgument("MaxLength", lengthVal.Max);

			string message;
			try {
				message = Options.GetUnformattedErrorMessage();
			}
			catch (NullReferenceException) {
				if (Validator is IExactLengthValidator) {
					message = cfg.LanguageManager.GetString("ExactLength_Simple");
				}
				else {
					message = cfg.LanguageManager.GetString("Length_Simple");
				}
			}


			if (message.Contains("{TotalLength}")) {
				if (Validator is IExactLengthValidator) {
					message = cfg.LanguageManager.GetString("ExactLength_Simple");
				}
				else {
					message = cfg.LanguageManager.GetString("Length_Simple");
				}
			}

			message = formatter.BuildMessage(message);
			return message;
		}
	}
}
