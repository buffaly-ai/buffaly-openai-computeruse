/**
 * KCS Utility Module: Validators
 * ------------------------------------------------------------
 * Standard validation rules used throughout the admin portal. They
 * integrate with MasterPage form binding and emit consistent error
 * messages when paired with UserMessages. Compose these functions rather
 * than writing ad-hoc regex or manual checks.
 *
 * Key helpers
 *  - Validators.Required(value)
 *      Truthy guard that treats empty strings as invalid.
 *  - Validators.Length(min, max?)
 *      Returns a validator that enforces the provided bounds.
 *  - Validators.MakeRequired(config)
 *      (toward bottom of file) clones validation configurations and
 *      adds the required rule for a subset of properties.
 *
 * Usage example – attach to a form binding descriptor:
 * ```js
 * var rules = {
 *     Name: { Label: "Name", Validators: [Validators.Required] },
 *     Email: { Label: "Email", Validators: [Validators.Required, Validators.Email] }
 * };
 * Validators.MakeRequired(rules, ["Name"]);
 * ```
 */
var Validators = {

	Required: function (oElement) {
		return (ObjectUtil.HasValue(oElement) && null != oElement && !StringUtil.IsEmpty(new String(oElement)))
	},

	Length: function (iMinLength, iMaxLength) {

		if (ObjectUtil.HasValue(iMaxLength)) {
			return function (oElement) {
				return ((oElement.length >= iMinLength && oElement.length <= iMaxLength));
			};
		}
		else {
			return function (oElement) {
				return ((oElement.length >= iMinLength));
			};
		}
	},

	Range: function (iMin, iMax) {
		return function (oElement) {
			return ((parseFloat(oElement) >= iMin && parseFloat(oElement) <= iMax));
		}
	},

	Minimum: function (iMin) {
		return function (oElement) {
			return parseFloat(oElement) >= iMin;
		}
	},

	Maximum: function (iMax) {
		return function (oElement) {
			return parseFloat(oElement) <= iMax;
		}
	},

	Boolean: function (oElement) {
		return typeof oElement === "boolean";
	},

	Number: function (oElement) {
		return (!StringUtil.IsEmpty(oElement) && !isNaN(oElement));
	},

	Money: function (oElement, oObject, oProp) {
		var bResult = Validators.Number(CleanDouble(oElement));
		if (bResult)
			oObject[oProp] = CleanDouble(oElement);
		return bResult;
	},

	Percent: function (oElement) {
		return (Validators.Number(oElement) && parseFloat(oElement) >= 0);
	},

	Weight: function (oElement) {
		return (!isNaN(oElement));
	},

	Integer: function (oElement) {
		return (Validators.Number(oElement) && ObjectUtil.IsInt(oElement));
	},

	ID: function (oElement) {
		return (Validators.Integer(oElement) && ObjectUtil.ToInt(oElement) > 0);
	},

	Date: function (oElement) {
		return (ObjectUtil.IsDate(oElement));
	},

	Email: function (oElement) {
		var emailPattern = /^([a-zA-Z0-9_\-\.+]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$/;
		if (!Validators.Length(5, 255)(oElement))
			return false;

		if (!StringUtil.InString(oElement, ".") || !StringUtil.InString(oElement, "@"))
			return false;

		return true;
	},

	Phone: function (oElement) {
		return Validators.Length(1, 20)(oElement);
	},

	Zip: function (oElement, oElementParent) {
		if (ObjectUtil.HasValue(oElementParent) && null != oElementParent && ObjectUtil.HasValue(oElementParent.Country)) {
			if (["US", "United States"].includes(oElementParent.Country))
				return new RegExp(/^\d{5}(-\d{4})?$/).test(oElement);

			if (["CA", "Canada"].includes(oElementParent.Country))
				return new RegExp(/^[ABCEGHJKLMNPRSTVXY]{1}\d{1}[A-Z]{1} *\d{1}[A-Z]{1}\d{1}$/).test(oElement);
		}

		return Validators.Length(1, 40)(oElement);
	},

	CreditCardExpYear: function (oElement, oElementParent) {

		if (!Validators.Integer(oElement) || !Validators.Range(2000, 2099)(oElement))
			return false;

		return (parseInt(oElement, 10) >= parseInt(new Date().getFullYear(), 10));
	},

	CreditCardExpMonth: function (oElement, oElementParent) {

		if (!Validators.Integer(oElement) || !Validators.Range(1, 12)(oElement))
			return false;

		return true;
	},

	FileExtensions: function (sExts) {
		return function (oElement) {
			return (sExts.toLowerCase().indexOf(StringUtil.RightOfLast(oElement.toLowerCase(), ".")) != -1);
		}
	},

	//VDB Types
	Url: function (oElement) {
		return Validators.Length(1, 512)(new String(oElement));
	},

	Data: function (oElement) {
		if (!StringUtil.IsEmpty(oElement))
			return Json.IsValid(new String(oElement));
		return true;
	},

	String: function (oElement) {
		return Validators.Length(1, 255)(new String(oElement));
	},

	Image: function (oElement) {
		return Validators.Length(1, 255)(new String(oElement));
	},

	Text: function (oElement) {
		return Validators.Length(1)(new String(oElement));
	},

	Double: function (oElement) {
		return Validators.Number(oElement);
	},


	//For .NET Code

	Int32: function (oElement) {
		return Validators.Integer(oElement);
	},

	Decimal: function (oElement) {
		return Validators.Number(oElement);
	},

	DateTime: function (oElement) {
		return Validators.Date(oElement);
	},

	Array: function (oValidators) {
		return function (oElements) {
			if (ObjectUtil.GetType(oElements) != 'array')
				return false;

			var bValid = true;
			oElements.forEach(function (oElement) {
				oValidators.forEach(function (oValidator) {
					if (!oValidator(oElement)) {
						bValid = false;
					}
				});
			});

			return bValid;
		}
	},

	Object: function (oElement) {
		return ObjectUtil.GetType(oElement) == 'object';
	},

	Empty: function (oElement) {
		return (null == oElement || StringUtil.IsEmpty(new String(oElement)));
	},

	Validate: function (oObject, oValidators) {
		var bResult = true;
		oObject.ValidationErrors = [];
		oObject.ExtendedValidationErrors = {};
		for (var oProp in oValidators) {
			var oValidator = oValidators[oProp];
			if (ObjectUtil.GetType(oValidator) == "object" && ObjectUtil.HasValue(oValidator.Validators) && oValidator.Validators.length > 0) {
				var bRequired = false;

				for (var i = 0; i < oValidator.Validators.length && !bRequired; i++) {
					bRequired = (oValidator.Validators[i] == Validators.Required);
				}

				//If the item is required, or is not required but is present then validate it
				if (bRequired || (!bRequired && Validators.Required(oObject[oProp]))) {
					oValidator.Validators.forEach(function (oValidatorElement) {
						try {
							if (!oValidatorElement(oObject[oProp], oObject, oProp)) {
								bResult = false;
								if (!oObject.ValidationErrors.includes(oValidator.InvalidMessage))
									oObject.ValidationErrors.push(oValidator.InvalidMessage);
								if (!ObjectUtil.HasValue(oObject.ExtendedValidationErrors[oProp])) {
									oObject.ExtendedValidationErrors[oProp] = [oValidatorElement];
								}
								else {
									oObject.ExtendedValidationErrors[oProp].push(oValidatorElement);
								}
								oObject.Validators = oValidators;
							}
						}
						catch (err) {
							bResult = false;
							if (!oObject.ValidationErrors.includes(oValidator.InvalidMessage))
								oObject.ValidationErrors.push(oValidator.InvalidMessage);
							if (!ObjectUtil.HasValue(oObject.ExtendedValidationErrors[oProp])) {
								oObject.ExtendedValidationErrors[oProp] = [oValidatorElement];
							}
							else {
								oObject.ExtendedValidationErrors[oProp].push(oValidatorElement);
							}
							oObject.Validators = oValidators;
						}
					});
				}
			}
		}

		oObject.IsValidated = bResult;
		return bResult;

	},

	MakeRequired: function (oValidator) {
		if (!oValidator || !oValidator.Validators)
			return oValidator;

		var oCopy = Object.assign({}, oValidator);
		oCopy.Validators = oValidator.Validators.slice();

		oCopy.Validators.push(Validators.Required);
		oCopy.InvalidMessage = oValidator.InvalidMessage + " " + ValidatorDescriptions.Required();

		return oCopy;
	}
};



// Shared validation error message builders used across the admin portal.
var ValidatorDescriptions = {

	Required: function () {
		return "Value cannot be blank.";
	},

	Length: function (iMinLength, iMaxLength) {
		return "Value should be between " + iMinLength + " and " + iMaxLength + " characters.";
	},

	Range: function (iMin, iMax) {
		return "Value should be a number between " + iMin + " and " + iMax + ".";
	},

	Minimum: function (iMin) {
		return "Value should be a number at least " + iMin + ".";
	},

	Maximum: function (iMax) {
		return "Value should be a number no more than " + iMax + ".";
	},

	Boolean: function (oElement) {
		return "Value should be true or false.";
	},

	Number: function (oElement) {
		return "Value should be a number.";
	},

	Money: function (oElement, oObject, oProp) {
		return "Value should be a valid monetary value.";
	},

	Percent: function (oElement) {
		return "Value should be a valid percent value.";
	},

	Weight: function (oElement) {
		return "Value should be a valid weight.";
	},

	Integer: function (oElement) {
		return "Value should be a number without decimals.";
	},

	ID: function (oElement) {
		return "Value should reference a valid object.";
	},

	Date: function (oElement) {
		return "Value should be a valid date.";
	},

	Email: function (oElement) {
		return "Value should be an email address.";
	},

	Phone: function (oElement) {
		return "Value should be a valid phone number.";
	},

	Zip: function (oElement, oElementParent) {
		return "Value should be a zip / postal code.";
	},

	CreditCardExpYear: function (oElement, oElementParent) {
		return "Value should be a valid expiration year.";
	},

	CreditCardExpMonth: function (oElement, oElementParent) {
		return "Value should be a valid expiration month.";
	},

	FileExtensions: function (sExts) {
		return "Value should be a valid file extension.";
	},

	//For .NET Code
	String: function (oElement) {
		return "Value should be a string between 1 and 255 characters.";
	},

	Int32: function (oElement) {
		return "Value should be a number without decimals.";
	},

	Decimal: function (oElement) {
		return "Value should be a number.";
	},

	DateTime: function (oElement) {
		return "Value should be a valid date.";
	},

	Object: function (oElement) {
		return "Could not validate object";
	},

	Empty: function (oElement) {
		return "Value should be blank.";
	}
}
