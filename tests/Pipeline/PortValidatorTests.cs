using Shiron.Lib.Pipeline.Port.Builder;
using Shiron.Lib.Pipeline.Port.Validator;
using Shiron.Lib.Pipeline.Port.Base;
using Xunit;

namespace Shiron.Lib.Tests.Pipeline;

public class NumericPortValidatorTests {
    private static NumericPortValidator<int> CreateValidator(int? min = null, int? max = null, bool nullable = false) {
        var builder = new NumericPortBuilder<int>("test");
        if (min.HasValue) builder.Min(min.Value);
        if (max.HasValue) builder.Max(max.Value);
        if (nullable) builder.Nullable();
        return new NumericPortValidator<int>(builder);
    }

    [Fact]
    public void Validate_WithinRange_ReturnsNull() {
        var validator = CreateValidator(min: 0, max: 100);
        Assert.Null(validator.Validate(50));
    }

    [Fact]
    public void Validate_BelowMin_ReturnsErrorMessage() {
        var validator = CreateValidator(min: 10, max: 100);
        var error = validator.Validate(5);
        Assert.NotNull(error);
        Assert.Contains("5", error);
        Assert.Contains("below minimum", error);
        Assert.Contains("10", error);
    }

    [Fact]
    public void Validate_AboveMax_ReturnsErrorMessage() {
        var validator = CreateValidator(min: 0, max: 100);
        var error = validator.Validate(150);
        Assert.NotNull(error);
        Assert.Contains("150", error);
        Assert.Contains("exceeds maximum", error);
        Assert.Contains("100", error);
    }

    [Fact]
    public void Validate_ExactlyMin_ReturnsNull() {
        var validator = CreateValidator(min: 10, max: 100);
        Assert.Null(validator.Validate(10));
    }

    [Fact]
    public void Validate_ExactlyMax_ReturnsNull() {
        var validator = CreateValidator(min: 10, max: 100);
        Assert.Null(validator.Validate(100));
    }

    [Fact]
    public void Validate_NoConstraints_ValueZero_ReturnsNull() {
        var validator = CreateValidator();
        Assert.Null(validator.Validate(0));
    }

    [Fact]
    public void Validate_MinOnly_ReturnsNullWhenAbove() {
        var validator = CreateValidator(min: 5, max: 100);
        Assert.Null(validator.Validate(100));
    }

    [Fact]
    public void Validate_MinOnly_ReturnsErrorWhenBelow() {
        var validator = CreateValidator(min: 5);
        Assert.NotNull(validator.Validate(3));
    }

    [Fact]
    public void Validate_MaxOnly_ReturnsNullWhenBelow() {
        var validator = CreateValidator(max: 100);
        Assert.Null(validator.Validate(50));
    }

    [Fact]
    public void Validate_MaxOnly_ReturnsErrorWhenAbove() {
        var validator = CreateValidator(max: 100);
        Assert.NotNull(validator.Validate(200));
    }

    [Fact]
    public void Validate_ZeroValueInRange_ReturnsNull() {
        var validator = CreateValidator(min: -10, max: 10);
        Assert.Null(validator.Validate(0));
    }

    [Fact]
    public void Validate_NegativeInRange_ReturnsNull() {
        var validator = CreateValidator(min: -100, max: 0);
        Assert.Null(validator.Validate(-50));
    }
}

public class StringPortValidatorTests {
    private static StringPortValidator CreateValidator(int? minLength = null, int? maxLength = null, bool nullable = false) {
        var builder = new StringPortBuilder("test");
        if (minLength.HasValue) builder.MinLength(minLength.Value);
        if (maxLength.HasValue) builder.MaxLength(maxLength.Value);
        if (nullable) builder.Nullable();
        return new StringPortValidator(builder);
    }

    [Fact]
    public void Validate_WithinLengthRange_ReturnsNull() {
        var validator = CreateValidator(minLength: 1, maxLength: 10);
        Assert.Null(validator.Validate("hello"));
    }

    [Fact]
    public void Validate_BelowMinLength_ReturnsErrorMessage() {
        var validator = CreateValidator(minLength: 5, maxLength: 20);
        var error = validator.Validate("hi");
        Assert.NotNull(error);
        Assert.Contains("2", error);
        Assert.Contains("below minimum", error);
        Assert.Contains("5", error);
    }

    [Fact]
    public void Validate_AboveMaxLength_ReturnsErrorMessage() {
        var validator = CreateValidator(minLength: 1, maxLength: 5);
        var error = validator.Validate("hello world");
        Assert.NotNull(error);
        Assert.Contains("11", error);
        Assert.Contains("exceeds maximum", error);
        Assert.Contains("5", error);
    }

    [Fact]
    public void Validate_ExactlyMinLength_ReturnsNull() {
        var validator = CreateValidator(minLength: 5, maxLength: 10);
        Assert.Null(validator.Validate("hello"));
    }

    [Fact]
    public void Validate_ExactlyMaxLength_ReturnsNull() {
        var validator = CreateValidator(minLength: 1, maxLength: 5);
        Assert.Null(validator.Validate("hello"));
    }

    [Fact]
    public void Validate_NoConstraints_ReturnsNull() {
        var validator = CreateValidator();
        Assert.Null(validator.Validate("any string"));
    }

    [Fact]
    public void Validate_MinLengthOnly_ReturnsNullWhenAbove() {
        var validator = CreateValidator(minLength: 3);
        Assert.Null(validator.Validate("hello"));
    }

    [Fact]
    public void Validate_MinLengthOnly_ReturnsErrorWhenBelow() {
        var validator = CreateValidator(minLength: 10);
        Assert.NotNull(validator.Validate("short"));
    }

    [Fact]
    public void Validate_MaxLengthOnly_ReturnsNullWhenBelow() {
        var validator = CreateValidator(maxLength: 100);
        Assert.Null(validator.Validate("hello"));
    }

    [Fact]
    public void Validate_MaxLengthOnly_ReturnsErrorWhenAbove() {
        var validator = CreateValidator(maxLength: 3);
        Assert.NotNull(validator.Validate("hello"));
    }

    [Fact]
    public void Validate_NullWithNullableTrue_ReturnsNull() {
        var validator = CreateValidator(nullable: true);
        Assert.Null(validator.Validate(null));
    }

    [Fact]
    public void Validate_NullWithNullableFalse_ReturnsErrorMessage() {
        var validator = CreateValidator(nullable: false);
        var error = validator.Validate(null);
        Assert.NotNull(error);
        Assert.Contains("null", error);
        Assert.Contains("not nullable", error);
    }

    [Fact]
    public void Validate_EmptyStringWithinRange_ReturnsNull() {
        var validator = CreateValidator(minLength: 0, maxLength: 10);
        Assert.Null(validator.Validate(""));
    }
}

public class PassAllPortValidatorTests {
    private readonly PassAllPortValidator _validator = new();

    [Fact]
    public void Validate_NonNullValue_ReturnsNull() {
        Assert.Null(_validator.Validate("anything"));
    }

    [Fact]
    public void Validate_Null_ReturnsNull() {
        Assert.Null(_validator.Validate(null));
    }

    [Fact]
    public void Validate_BoxedInt_ReturnsNull() {
        Assert.Null(_validator.Validate(42));
    }
}
