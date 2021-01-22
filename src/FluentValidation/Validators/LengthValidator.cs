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
	using Resources;

	public class LengthValidator<T> : ICustomValidator<T,string>, ILengthValidator {
		public int Min { get; }
		public int Max { get; }

		public Func<T, int> MinFunc { get; set; }
		public Func<T, int> MaxFunc { get; set; }

		public LengthValidator(int min, int max) {
			Max = max;
			Min = min;

			if (max != -1 && max < min) {
				throw new ArgumentOutOfRangeException(nameof(max), "Max should be larger than min.");
			}
		}

		public LengthValidator(Func<T, int> min, Func<T, int> max) {
			MaxFunc = max;
			MinFunc = min;
		}

		public virtual void Configure(ICustomRuleBuilder<T, string> rule) => rule
			.Custom(IsValid)
			.WithErrorCode("LengthValidator")
			.WithMessageFromLanguageManager("LengthValidator");


		protected void IsValid(IPropertyValidatorContext<T,string> context) {
			if (context.PropertyValue == null) return;

			var min = Min;
			var max = Max;

			if (MaxFunc != null && MinFunc != null) {
				max = MaxFunc(context.InstanceToValidate);
				min = MinFunc(context.InstanceToValidate);
			}

			int length = context.PropertyValue.Length;

			if (length < min || (length > max && max != -1)) {
				context.MessageFormatter
					.AppendArgument("MinLength", min)
					.AppendArgument("MaxLength", max)
					.AppendArgument("TotalLength", length);

				context.AddFailure();
			}
		}
	}

	public class ExactLengthValidator<T> : LengthValidator<T>, IExactLengthValidator {
		public ExactLengthValidator(int length) : base(length,length) {
		}

		public ExactLengthValidator(Func<T, int> length)
			: base(length, length) {
		}

		public override void Configure(ICustomRuleBuilder<T, string> rule) => rule
			.Custom(IsValid)
			.WithErrorCode("ExactLengthValidator")
			.WithMessageFromLanguageManager("ExactLengthValidator");
	}

	public class MaximumLengthValidator<T> : LengthValidator<T>, IMinimumLengthValidator {
		public MaximumLengthValidator(int max)
			: base(0, max) {
		}

		public MaximumLengthValidator(Func<T, int> max)
			: base(obj => 0, max) {
		}

		public override void Configure(ICustomRuleBuilder<T, string> rule) => rule
			.Custom(IsValid)
			.WithErrorCode("MaximumLengthValidator")
			.WithMessageFromLanguageManager("MaximumLengthValidator");
	}

	public class MinimumLengthValidator<T> : LengthValidator<T>, IMaximumLengthValidator {

		public MinimumLengthValidator(int min)
			: base(min, -1) {
		}

		public MinimumLengthValidator(Func<T, int> min)
			: base(min, obj => -1) {
		}

		public override void Configure(ICustomRuleBuilder<T, string> rule) => rule
			.Custom(IsValid)
			.WithErrorCode("MinimumLengthValidator")
			.WithMessageFromLanguageManager("MinimumLengthValidator");

	}

	public interface ILengthValidator : ICustomValidator {
		int Min { get; }
		int Max { get; }
	}

	public interface IMaximumLengthValidator : ILengthValidator { }

	public interface IMinimumLengthValidator : ILengthValidator { }

	public interface IExactLengthValidator : ILengthValidator { }
}
