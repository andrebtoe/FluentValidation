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
	using System.Linq;

	public class StringEnumValidator<T> : ICustomValidator<T, string> {
		private readonly Type _enumType;
		private readonly bool _caseSensitive;

		public StringEnumValidator(Type enumType, bool caseSensitive) {
			if (enumType == null) throw new ArgumentNullException(nameof(enumType));

			CheckTypeIsEnum(enumType);

			_enumType = enumType;
			_caseSensitive = caseSensitive;
		}

		public void Configure(ICustomRuleBuilder<T, string> rule) => rule
			.Custom(Validate)
			.WithErrorCode("StringEnumValidator")
			.WithMessageFromLanguageManager("EnumValidator"); // Intentionally the same message as EnumValidator.

		protected void Validate(IPropertyValidatorContext<T,string> context) {
			if (context.PropertyValue == null) return;
			var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			bool valid = Enum.GetNames(_enumType).Any(n => n.Equals(context.PropertyValue, comparison));
			if (!valid) {
				context.AddFailure();
			}
		}

		private static void CheckTypeIsEnum(Type enumType) {
			if (!enumType.IsEnum) {
				string message = $"The type '{enumType.Name}' is not an enum and can't be used with IsEnumName.";
				throw new ArgumentOutOfRangeException(nameof(enumType), message);
			}
		}
	}
}
