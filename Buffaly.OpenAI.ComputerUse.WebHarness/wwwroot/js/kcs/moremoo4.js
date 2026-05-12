/**
 * KCS Utility Module: moremoo4
 * ------------------------------------------------------------
 * Core polyfills and helper surfaces that keep legacy MooTools-era
 * pages working while presenting modern, well-documented entry points.
 * This file defines the root namespaces (MoreMoo, ControlUtil, ObjectUtil,
 * StringUtil, etc.) consumed throughout the portal. Reach for these
 * utilities before writing bespoke DOM or string helpers.
 *
 * Highlighted helpers
 *  - ControlUtil.GetControl(idOrElement) -> Safe element wrapper used by _$(...).
 *  - ControlUtil.AddEvent(el, event, handler) -> Cross-browser event wiring.
 *  - StringUtil.IsEmpty(value) / Clean(value) / InString(haystack, needle) ->
 *      Standardised string checks and comparisons.
 *  - ObjectUtil.HasValue(value) -> Truthy/null/undefined guard used everywhere.
 *
 * Usage example – safe element access and event registration:
 * ```js
 * _$("btnSave")
 *     .removeClass("Hidden")
 *     .addEvent("click", function() {
 *         if (!Validators.Required(_$("txtName").getValue())) {
 *             UserMessages.DisplayNow("Name is required", "Error");
 *             return;
 *         }
 *         ControlUtil.ToggleSpinnerOn(_$("btnSave"));
 *     });
 * ```
 *
 * Usage example – leverage StringUtil helpers in validation:
 * ```js
 * if (StringUtil.IsEmpty(model.Email) || !StringUtil.InString(model.Email, "@")) {
 *     UserMessages.DisplayNow("Enter a valid email address", "Warning");
 * }
 * ```
 */
"use strict";

/**
 * Root namespace that bridges legacy MooTools helpers with the FairPath utility set.
 */
var MoreMoo = {
	version: '3.0'
};

//Preserved from mootools

var typeOf = this.typeOf = function (item) {
	if (item == null) return 'null';
	if (item.constructor === Array) return 'array';
	if (item.nodeName) {
		if (item.nodeType == 1) return 'element';
		if (item.nodeType == 3) return (/\S/).test(item.nodeValue) ? 'textnode' : 'whitespace';
	} else if (typeof item.length == 'number') {
		//	if (item.callee) return 'arguments';
		//if ('item' in item) return 'collection';
	}

	return typeof item;
};
/**
 * Lightweight JSON helpers for defensive parsing in legacy call sites.
 */
var Json = {

	IsValid: function (str) {
		try {
			var oObj = JSON.parse(str);
		}
		catch (err) {
			return false
		}
		return true;
	}

};

//Conflicts with some other library
Array.from2 = function (item) {
	if (item == null) return [];
	return (ObjectUtil.IsEnumerable(item) && typeof item != 'string') ? (typeOf(item) == 'array') ? item : Array.prototype.slice.call(item) : [item];
};

// Preserve legacy iteration alias used throughout the admin scripts
Array.prototype.each = Array.prototype.forEach;



Element.NativeEvents = {
	click: 2, dblclick: 2, mouseup: 2, mousedown: 2, contextmenu: 2, //mouse buttons
	mousewheel: 2, DOMMouseScroll: 2, //mouse wheel
	mouseover: 2, mouseout: 2, mousemove: 2, selectstart: 2, selectend: 2, //mouse movement
	keydown: 2, keypress: 2, keyup: 2, //keyboard
	orientationchange: 2, // mobile
	touchstart: 2, touchmove: 2, touchend: 2, touchcancel: 2, // touch
	gesturestart: 2, gesturechange: 2, gestureend: 2, // gesture
	focus: 2, blur: 2, change: 2, reset: 2, select: 2, submit: 2, paste: 2, input: 2, //form elements
	load: 2, unload: 1, beforeunload: 2, resize: 1, move: 1, DOMContentLoaded: 1, readystatechange: 1, //window
	error: 1, abort: 1, scroll: 1 //misc
};

Element.Properties = {};


/**
 * Mathematical helpers that support combinatorics and precision rounding for grid features.
 */
var MathUtil = {
	factorial: function (n) {
		if (n <= 1)
			return 1;

		return n * MathUtil.factorial(n - 1);
	},

	nPk: function (n, k) {
		return (MathUtil.factorial(n) / MathUtil.factorial(n - k));
	},

	nCk: function (n, k) {
		return (MathUtil.factorial(n) / (MathUtil.factorial(k) * MathUtil.factorial(n - k)));
	},

	roundPlaces: function (num, dec) {
		return Math.round(num * Math.pow(10, dec)) / Math.pow(10, dec);
	}
};


/**
 * Extensions for working with standard arrays without mutating the original values.
 */
var ArrayUtil = {

	ForEachReverse: function (arr, fn, bind) {
		if (!Array.isArray(arr) || typeof fn !== 'function')
			return;

		for (var i = arr.length - 1; i >= 0; i--) {
			fn.call(bind, arr[i], i, arr);
		}
	},

	ReverseMap: function (arr, fn, bind) {
		if (!Array.isArray(arr) || typeof fn !== 'function')
			return [];

		var results = [];
		for (var i = arr.length - 1, j = 0; i >= 0; i--, j++) {
			results[j] = fn.call(bind, arr[i], i, arr);
		}

		return results;
	},

	RemoveAll: function (arr, oItem) {
		if (!Array.isArray(arr))
			return arr;

		for (var i = arr.length - 1; i >= 0; i--) {
			if (arr[i] === oItem)
				arr.splice(i, 1);
		}

		return arr;
	},

	QueueFront: function (queue, item, length, predicate) {
		var list = Array.isArray(queue) ? queue.slice() : [];
		var filterFn = (typeof predicate === 'function') ? predicate : function (candidate, existing) { return candidate !== existing; };

		list = list.filter(function (x) { return filterFn(item, x); });
		list.unshift(item);

		if (typeof length === 'number' && list.length > length)
			list = list.slice(0, length);

		return list;
	}

};


/**
 * Core object/value helpers that back most validation and DOM glue code.
 */
var ObjectUtil = {

	ShallowClone: function (oObj) {
		return Object.assign({}, oObj);
	},
	Clone: function (oObj) {
		if (!ObjectUtil.HasValue(oObj))
			return oObj;

		return JSON.parse(JSON.stringify(oObj));
	},
	/**
	 * Tries to execute a series of functions and returns the result of the first one
	 * that does not throw an error.
	 */
	Attempt: function () {
		for (var i = 0, l = arguments.length; i < l; i++) {
			try {
				return arguments[i]();
			} catch (e) { }
		}
		return null;
	},
	IsDefined: function (oObj) {
		return !(oObj === undefined);
	},
	HasValue: function (oObj) {
		return null != oObj;
	},

	ForEach: function (object, fn, bind) {
		if (!object || typeof fn !== 'function')
			return;

		Object.keys(object).forEach(function (key) {
			fn.call(bind, object[key], key, object);
		});
	},

	IsFunction: function (oObj) {
		return ObjectUtil.HasValue(oObj) && ObjectUtil.GetType(oObj) == 'function';
	},

	/**
	 * Wraps a callback-based service call in a Promise so async/await flows stay consistent.
	 * The helper appends the success callback as the last argument before invoking the method.
	 */
	Promisify: function (context, method, argsArray) {
		if (!ObjectUtil.IsFunction(method))
			return Promise.reject(new Error('ObjectUtil.Promisify requires a function.'));

		var args = Array.isArray(argsArray) ? argsArray.slice() : [];

		return new Promise(function (resolve, reject) {
			try {
				args.push(function (result) {
					resolve(result);
				});

				method.apply(context, args);
			} catch (err) {
				reject(err);
			}
		});
	},

	IsEnumerable: function (item) {
		return (item != null && typeof item.length === 'number' && toString.call(item) != '[object Function]');
	},

	ToInt: function (oObj) {
		if (ObjectUtil.GetType(oObj) == 'string')
			return parseInt(oObj);

		if (!isNaN(oObj))
			return oObj;

		throw new Exception("Could not convert object to integer: " + JSON.stringify(oObj));
	},

	AreEqual: function (oObj1, oObj2) {
		// Create arrays of property names
		var aProps = Object.getOwnPropertyNames(oObj1);
		var bProps = Object.getOwnPropertyNames(oObj2);

		// If number of properties is different,
		// objects are not equivalent
		if (aProps.length != bProps.length) {
			return false;
		}

		for (var i = 0; i < aProps.length; i++) {
			var propName = aProps[i];

			if (ObjectUtil.GetType(oObj1[propName]) == 'object') {
				if (!ObjectUtil.AreEqual(oObj1[propName], oObj2[propName]))
					return false;
			}

			// If values of same property are not equal,
			// objects are not equivalent
			else if (oObj1[propName] !== oObj2[propName]) {
				return false;
			}
		}

		// If we made it this far, objects
		// are considered equivalent
		return true;
	},

	IsEmpty: function (oObj) {
		return ObjectUtil.HasValue(oObj) && Object.keys(oObj).length == 0;
	},

	GetType: function (object) {
		var type = typeOf(object);
		if (object instanceof String) return 'string';
		if (object instanceof Date) return 'date';
		if (type == 'elements') return 'array';
		return (type == 'null') ? false : type;
	},

	Append: function (original) {
		for (var i = 1, l = arguments.length; i < l; i++) {
			var extended = arguments[i] || {};
			for (var key in extended) original[key] = extended[key];
		}
		return original;
	},

	IsInt: function (value) {
		if (!ObjectUtil.HasValue(value))
			return false;

		var parsed = parseInt(value);
		if (isNaN(parsed))
			return false;

		return String(parsed) == String(value) || Number(value) === parsed;
	},

	IsDate: function (value) {
		if (!ObjectUtil.HasValue(value))
			return false;

		var scratch = new Date(value);
		return (scratch.toString() != "NaN" && scratch.toString() != "Invalid Date");
	},

	Merge: function (obj1, obj2) {
		return $merge(obj1, obj2);
	}
}

/**
 * String helpers centralised for consistent parsing, searching, and formatting.
 */
var StringUtil = {
	Between: function (strSource, strLeftTarget, strRightTarget, bNarrow) {
		bNarrow = ObjectUtil.HasValue(bNarrow) ? bNarrow : false;
		if (!bNarrow) {
			return StringUtil.LeftOfLast(StringUtil.RightOfFirst(strSource, strLeftTarget), strRightTarget);
		}
		else {
			return StringUtil.LeftOfFirst(StringUtil.RightOfFirst(strSource, strLeftTarget), strRightTarget);
		}
	},

	TrimQuotes: function (strSource) {
		var strValue = new String(strSource);
		var cQuotes = ['\'', '\"'];
		if (!StringUtil.IsEmpty(strValue) &&
			cQuotes.includes(strValue.charAt(0)) &&
			cQuotes.includes(strValue.charAt(strValue.length - 1)) &&
			strValue.charAt(0) == strValue.charAt(strValue.length - 1)) {
			return strValue.substr(1, strValue.length - 2);
		}

		return strValue;
	},

	Trim: function (strSource) {
		return strSource ? strSource.trim() : '';
	},

	Clean: function (strSource) {
		if (!ObjectUtil.HasValue(strSource))
			return '';

		return String(strSource).replace(/\s+/g, ' ').trim();
	},

	IsEmpty: function (strSource) {
		return (!ObjectUtil.HasValue(strSource) || new String(strSource).trim() == "")
	},

	EqualNoCase: function (str1, str2) {
		return (new String(str1).trim().toLowerCase() === new String(str2).trim().toLowerCase());
	},

	Format: function (strFormat) {
		for (var i = 1, len = arguments.length; i < len; i++) {
			var s = "{" + (i - 1) + "}";
			var iIndex = 0;
			while (iIndex < (iIndex = strFormat.indexOf(s)))
				strFormat = strFormat.replace(s, arguments[i]);
		}
		return strFormat;
	},

	LeftOfLast: function (strSource, strTarget) {
		if (null == strSource)
			return "";

		var strResult = '';
		var iPos = strSource.toLowerCase().lastIndexOf(strTarget.toLowerCase());
		if (-1 == iPos)
			strResult = strSource;
		else
			strResult = strSource.substr(0, iPos);

		return strResult;
	},

	RightOfLast: function (strSource, strTarget) {
		if (null == strSource)
			return "";

		var strResult = '';
		var iPos = strSource.toLowerCase().lastIndexOf(strTarget.toLowerCase());

		if (-1 == iPos)
			strResult = "";
		else
			strResult = strSource.substr(iPos + strTarget.length);

		return strResult;
	},

	LeftOfFirst: function (strSource, strTarget) {
		if (null == strSource)
			return "";

		var strResult = '';
		var iPos = strSource.toLowerCase().indexOf(strTarget.toLowerCase());
		if (-1 == iPos)
			strResult = strSource;
		else
			strResult = strSource.substr(0, iPos);

		return strResult;
	},

	RightOfFirst: function (strSource, strTarget) {
		if (null == strSource)
			return "";

		var strResult = '';
		var iPos = strSource.toLowerCase().indexOf(strTarget.toLowerCase());

		if (-1 == iPos)
			strResult = "";
		else
			strResult = strSource.substr(iPos + strTarget.length);

		return strResult;
	},

	InString: function (strSource, strTarget) {
		if (null == strSource)
			return false;

		if (ObjectUtil.GetType(strSource) != 'string' || ObjectUtil.GetType(strTarget) != 'string')
			return false;

		return (strSource.toLowerCase().indexOf(strTarget.toLowerCase()) != -1);
	},

	ContainsWord: function (strSource, strTarget, separator) {
		if (!ObjectUtil.HasValue(strSource) || !ObjectUtil.HasValue(strTarget))
			return false;

		if (!ObjectUtil.HasValue(separator) || separator === '')
			return StringUtil.InString(String(strSource), String(strTarget));

		var strSeparator = String(separator);
		var strWrappedSource = strSeparator + String(strSource) + strSeparator;
		var strWrappedTarget = strSeparator + String(strTarget) + strSeparator;
		return strWrappedSource.indexOf(strWrappedTarget) > -1;
	},

	NormalizeCase: function (str) {
		if (StringUtil.IsEmpty(str) || ObjectUtil.GetType(str) != "string")
			return "";

		return str.toLowerCase().replace(/\b\w/g, function (char) {
			return char.toUpperCase();
		});
	},

	//pads left 000005
	PadLeft: function (str, padString, length) {
		str = new String(str);
		while (str.length < length)
			str = padString + str;
		return str;
	},

	//pads right 50000
	PadRight: function (str, padString, length) {
		str = new String(str);
		while (str.length < length)
			str = str + padString;
		return str;
	},

	StartsWith: function (str, target) {
		if (null == str)
			return false;

		return str.toLowerCase().indexOf(target.toLowerCase()) == 0;
	},


	EndsWith: function (str, target) {
		if (null == str)
			return false;

		return (str.length >= target.length && str.toLowerCase().lastIndexOf(target.toLowerCase()) == str.length - target.length);
	},

	ReplaceAll: function (str, toRemove, toInsert) {
		return new String(str).replace(new RegExp(toRemove, "g"), toInsert);
	},

	ReplaceAllEx: function (str, toRemove, toInsert) {
		//Case insensitive version
		return new String(str).replace(new RegExp(toRemove, "gi"), toInsert);
	},

	Split: function (str, splitOn) {
		var oStrs = str.split(splitOn);
		var oStrs2 = [];
		oStrs.forEach(function (oStr) {
			var s = oStr.trim();
			if (!StringUtil.IsEmpty(s))
				oStrs2.push(s);
		});
		return oStrs2;
	},

	RemoveAt: function (strSource, iIndex, iLength) {
		if (!ObjectUtil.HasValue(strSource))
			return '';

		var strValue = String(strSource);
		var length = ObjectUtil.HasValue(iLength) ? iLength : 1;
		return strValue.substring(0, iIndex) + strValue.substring(iIndex + length);
	},

	InsertAt: function (strSource, iIndex, strInsert) {
		var strValue = ObjectUtil.HasValue(strSource) ? String(strSource) : '';
		var strToInsert = ObjectUtil.HasValue(strInsert) ? String(strInsert) : '';
		return strValue.substring(0, iIndex) + strToInsert + strValue.substring(iIndex);
	},

	ToFloatEx: function (strSource) {
		if (!ObjectUtil.HasValue(strSource))
			return NaN;

		return parseFloat(String(strSource).replace('$', ''));
	},

	ToBoolean: function (str) {
		str = new String(str).toLowerCase();
		return (['true', "1", "-1", "yes", "y"].includes(str));
	},

	UppercaseFirstLetter: function (strValue) {
		if (null == strValue)
			return "";

		var strResult = "";
		for (var i = 0; i < strValue.length; i++) {
			if (i == 0)
				strResult += strValue[i].toUpperCase();
			else
				strResult += strValue[i].toLowerCase();
		}

		return strResult;
	},

	/**
	 * Generates a modern GUID/UUID using crypto when available, with fallbacks for older browsers.
	 */
	GenerateGUID: function () {
		var root = (typeof globalThis !== 'undefined') ? globalThis : (typeof window !== 'undefined' ? window : this);

		if (root && root.crypto && ObjectUtil.IsFunction(root.crypto.randomUUID))
			return root.crypto.randomUUID();

		if (root && root.uuidv4 && ObjectUtil.IsFunction(root.uuidv4)) {
			try {
				return root.uuidv4();
			} catch (err) { }
		}

		var rand = Math.floor(Math.random() * 0xFFFFFFFFFFFF).toString(16);
		return 'token-' + Date.now().toString(36) + '-' + rand;
	},

	UID: Date.now(),

	UniqueID: function () {
		return (StringUtil.UID++).toString(36);
	},

	IsUpperCase: function (value) {
		return (value == new String(value).toUpperCase());
	},

	IsLowerCase: function (value) {
		return (value == new String(value).toLowerCase());
	},

	InsertSpacesInWords: function (value) {
		var sbResult = new String();
		for (var i = 0; i < value.length; i++) {
			var c = value[i];
			if (sbResult.length == 0 || !StringUtil.IsUpperCase(c) || sbResult[sbResult.length - 1] == ' ' || StringUtil.IsUpperCase(sbResult[sbResult.length - 1]))
				sbResult += c;
			else
				sbResult += " " + c;
		}
		return sbResult;
	}
};
/**
 * URL parsing helpers used across navigation and local storage helpers.
 */
var UrlUtil = {

	GetUrlWithoutParams: function (sPage) {
		var sRoot;
		if (sPage.indexOf("?") != -1) {
			sRoot = StringUtil.LeftOfFirst(sPage, "?");
		}

		else
			sRoot = sPage;

		return sRoot;
	},

	GetQueryString: function (sPage) {
		var sQueryString;
		if (sPage.indexOf("?") != -1) {
			sQueryString = StringUtil.RightOfFirst(sPage, "?");

			if (sQueryString.indexOf("#") != -1)
				sQueryString = StringUtil.LeftOfFirst(sQueryString, "#");
		}

		else
			sQueryString = '';

		return sQueryString;
	},

	GetDirectory: function (sPage) {
		var sRoot = UrlUtil.GetUrlWithoutParams(sPage);
		return StringUtil.LeftOfLast(sRoot, "/");
	},

	GetFile: function (sPage) {
		var sRoot = UrlUtil.GetUrlWithoutParams(sPage);
		if (StringUtil.InString(sRoot, "/"))
			sRoot = StringUtil.RightOfLast(sRoot, "/");
		return sRoot;
	},

	GetFileExtension: function (sPage) {
		var sRoot = UrlUtil.GetFile(sPage);
		if (StringUtil.InString(sRoot, "."))
			sRoot = "." + StringUtil.RightOfFirst(sRoot, ".");
		else
			sRoot = "";

		return sRoot;
	},

	ToQueryString: function (obj) {
		var queryString = [];
		for (var i in obj) {
			var name = i;
			var value = obj[i];
			if (null != value)
				queryString.push(name + '=' + encodeURIComponent(value));
		}
		return queryString.join('&');
	},

	FromQueryString: function (str, decodeKeys, decodeValues) {
		if (decodeKeys == null) decodeKeys = true;
		if (decodeValues == null) decodeValues = true;

		var object = {};

		if (!str || str === '')
			return object;

		str.split(/[&;]/).forEach(function (val) {
			if (val === '') return;
			var index = val.indexOf('=') + 1,
				value = index ? val.substr(index) : '',
				keys = index ? val.substr(0, index - 1).match(/([^\]\[]+|(\B)(?=\]))/g) : [val],
				obj = object;
			if (!keys) return;
			if (decodeValues) value = decodeURIComponent(value);
			keys.forEach(function (key, i) {
				if (decodeKeys) key = decodeURIComponent(key);
				var current = obj[key];

				if (i < keys.length - 1) obj = obj[key] = current || {};
				else if (typeOf(current) == 'array') current.push(value);
				else obj[key] = current != null ? [current, value] : value;
			});
		});

		return object;
	}
}


const NumberUtil = {
	FormatNumber: function (value) {
		if (typeof value !== 'number') {
			value = parseFloat(value) || 0;
		}
		return new Intl.NumberFormat('en-US').format(value);
	}
};


var PathUtil = {

	GetFile: function (sPath) {
		if (StringUtil.InString(sPath, "\\"))
			sPath = StringUtil.RightOfLast(sPath, "\\");
		return sPath;
	}

}


//$$$ - get abstract elements
//	$$$(string) -> new Element(string)
//  $$$(string, object) -> new Element(string, object)
//	$$$(object) -> object
//	$$$(string, string) -> new Element(string).setHTML(string)
//	$$$(string, object, string) -> new Element(string, object).setHTML(string)
//	$$$(string, object, array) -> new Element(string, object) 
function $$$(oStm) {
	var oResult = null;
	try {
		oResult = _$$$(oStm);
	}
	catch (err) {
		Page.LogError(err);
		Page.LogError($$$.Write(oStm));
		throw err;
	}
	return oResult;
}

function _$$$(oStm) {
	var oEl = null;

	//Process nested statements first
	var iLength = oStm.length;
	for (var i = 0; i < iLength; i++) {
		var oSub = oStm[i];
		switch (ObjectUtil.GetType(oSub)) {
			case 'array':
				if (oSub.length > 0 && ObjectUtil.GetType(oSub[0]) == 'array') {
					oSub.forEach(function (oSubEl) {
						let x = _$$$(oSubEl);
						if (x) oEl.appendChild(x);
					})
				}
				else if (oSub.length > 0) {
					let x = _$$$(oSub);
					if (null != oEl)
						oEl.appendChild(x);
					else
						throw "$$$ failed to create array of children because there is no parent";
				}

				break;
			case 'number':
			case 'string':
				if (i == 0) {
					oEl = document.createElement(oSub);
				}
				else
					oEl.innerHTML = oSub.toString();
				break;
			case 'element':
				oEl.appendChild(oSub);
				break;
			case 'object':
				if (i == 0) {
					Page.Trace($$$.Write(oSub));
					Page.Trace($$$.Write(oStm));
					throw 'error';
				}
				else if (i == 1) {
					for (var prop in oSub) {
						var index = Element.Properties[prop];
						if (index) {
							//mootools requires style to be an object
							if (prop == 'style' && ObjectUtil.GetType(oSub[prop] == 'string'))
								oEl.style.cssText = oSub[prop];
							else
								oEl[index] = oSub[prop];
						}
						else if (Element.NativeEvents[prop]) {
							ControlUtil.AddEvent(oEl, prop, oSub[prop]);
						}
						else {
							var oAttr = oSub[prop];
							if (!ObjectUtil.HasValue(oAttr))
								throw prop + " is not defined";
							oEl.setAttribute(prop, oAttr.toString());
						}
					}
				}
				break;
			case false:
				if (i == 0)
					throw 'error';
				//else
				//oEl = 'undefined';
				break;
		}
	}
	return oEl;
}

var iLevels = 0;
$$$.Write = function (oStm) {
	iLevels++;
	var strRes = "[";
	for (var i = 0; i < oStm.length; i++) {
		if (i > 0)
			strRes += ", ";
		if (ObjectUtil.GetType(oStm[i]) == 'array') {
			strRes += "<br/>";
			for (var j = 0; j < iLevels; j++)
				strRes += "&nbsp;&nbsp;"

			strRes += $$$.Write(oStm[i]);
		}
		else if (ObjectUtil.GetType(oStm[i]) == false)
			strRes += 'undefined';
		else
			strRes += JSON.stringify(oStm[i]);

	}
	strRes += "]";
	iLevels--;
	return strRes;
};




/**
 * Minimal event emitter used by grid collections and other legacy models.
 */
class _Events {

	_events = {};

	addEvent(sName, f) {
		if (this._events[sName] == null)
			this._events[sName] = [];

		this._events[sName].push(f);
	}

	fireEvent(sName, arg) {
		if (this._events[sName] != null)
			this._events[sName].forEach(evt => evt(arg));
	}
}



/**
 * Observable collection wrapper that notifies the UI when data changes.
 */
class GridCollection extends _Events {

	Insert(sGridName, oGrid) {
		this[sGridName] = oGrid;
		this.fireEvent("grid-inserted", oGrid);
		this.Names.push(sGridName);
	};

	Names = [];

	At(i) {
		return this[this.Names[i]];
	};
}


var Page = {
	IsUnloading: false,

	m_bOnloadRun: false,

	AddOnload: function (oVar) {
		if (Page.m_bOnloadRun) { //Scripts loaded after page load should still run correctly
			if (Globals.ReportErrors) {
				try {
					oVar();
				}
				catch (err) {
					Page.HandleUnexpectedError(err);
				}
			}
			else
				oVar();
		}
		else {
			document.addEventListener('DOMContentLoaded', function () {
				Page.m_bOnloadRun = true;
				if (Globals.ReportErrors) {
					try {
						oVar();
					}
					catch (err) {
						Page.HandleUnexpectedError(err);
					}
				}
				else
					oVar();
			});
		}
	},


	AddOnUnload: function (oVar) {
		//window.onbeforeunload = oVar;
		window.addEventListener("beforeunload", oVar);
	},

	AddOnError: function (oFunc) {
		window.onerror = oFunc;
	},

	LogError: function (oError) {
		ErrorLogging.LogError(oError);
		Page.Trace(oError);
		return oError;
	},

	LogEvent: function (sEvent, sMessage) {
		ErrorLogging.LogEvent(sEvent, sMessage);
	},

	Trace: function (msg) {
		try {
			if (ObjectUtil.HasValue(console) && ObjectUtil.HasValue(console.log))
				console.log(msg);

		}
		catch (err) {

		}
	},

	Reload: function (oQueryParams) {
		if (!ObjectUtil.HasValue(oQueryParams))
			document.location.reload();
		else
			Page.Redirect(document.location.href, oQueryParams);
	},

	LogErrorToConsole: function (err) {
		let e = err;

		// Case 1: wrapper object like { Error: <Error> }
		if (e && e.Error instanceof Error) {
			e = e.Error;
		}
		// Case 2: already an Error
		else if (!(e instanceof Error)) {
			// Case 3: string or something else
			if (typeof e === "string") {
				e = new Error(e);
			} else {
				try {
					e = new Error(JSON.stringify(e));
				} catch {
					e = new Error(String(e));
				}
			}
		}

		// Normalize message for ignore logic (.message vs .Message)
		const msg = e.Message || e.message;
		if (msg && ErrorLogging && typeof ErrorLogging.IsIgnored === "function") {
			if (ErrorLogging.IsIgnored(msg)) {
				return;
			}
		}

		// Keep Message in sync for any code expecting .Message
		if (!e.Message && msg) {
			e.Message = msg;
		}

		// THIS is the important part: log the real Error, not the wrapper
		console.error(e);
	},

	HandleUnexpectedError: function (err) {

		// 1) Normalize to a JS Error where possible
		let jsErr = null;

		if (err instanceof Error) {
			jsErr = err;
		} else if (err && err.Error instanceof Error) {
			jsErr = err.Error;
		} else if (typeof err === "string") {
			jsErr = new Error(err);
		}

		const primaryMessage =
			(jsErr && (jsErr.Message || jsErr.message)) ||
			(err && (err.Message || err.message)) ||
			"";

		// 2) Ignore filter based on the message string
		if (primaryMessage && ErrorLogging && typeof ErrorLogging.IsIgnored === "function") {
			if (ErrorLogging.IsIgnored(primaryMessage)) {
				return;
			}
		}

		// 3) Log the JS error to console (prefer the normalized one)
		if (Page && typeof Page.LogErrorToConsole === "function") {
			Page.LogErrorToConsole(jsErr || err);
		} else {
			console.error(jsErr || err);
		}

		// 4) Grab LastError (window.onerror hacks)
		if (Page && Page.LastError) {
			err = Page.LastError;
			Page.LastError = null;
			if (!jsErr && err instanceof Error) {
				jsErr = err;
			}
		}

		// 5) Detect "true" JsonWs / server errors:
		//    - outer object has .Error
		//    - and that .Error is NOT an Error instance
		const hasServerPayload =
			err &&
			ObjectUtil.HasValue(err.Error) &&
			!(err.Error instanceof Error);

		if (hasServerPayload) {
			// JsonWs errors, don't send back to the server (they came from the server)
			let serverError = err.Error;

			if (typeof serverError === "string") {
				try {
					serverError = JSON.parse(serverError);
				} catch {
					// leave as string
				}
			}

			// Some services wrap twice: { Error: { Error: "..." } }
			if (serverError && serverError.Error) {
				serverError = serverError.Error;
			}

			if (serverError === "Invalid data") {
				UserMessages.DisplayNow(err.Data && err.Data.ValidationErrors, "Error");
			}
			else if (typeof serverError === "string" && StringUtil.InString(serverError, "Session Expired")) {
				UserMessages.DisplayNow("Session expired.", "Error");
			}
			else {
				UserMessages.DisplayNow(serverError, "Error");
			}

			// NOTE: intentionally NOT calling Page.LogError here – it's server-originated.
			return;
		}

		// 6) Remaining cases: JS errors (plain Error, string, etc.)
		if (
			(ObjectUtil.HasValue(primaryMessage) && StringUtil.InString(primaryMessage, "Invalid data")) ||
			(ObjectUtil.GetType(err) === "string" && StringUtil.InString(err, "Invalid data"))
		) {
			// Validation errors are handled by Page.HandleValidationErrors
			Page.Trace && Page.Trace("Validation error caught");
			return;
		}

		// 7) Generic JS error display + logging
		const errorForInfo = jsErr || err;

		if (Globals.ReturnExceptions === true) {
			const oError = Exception.GetErrorInfo(errorForInfo);

			const type = oError.Type ?? "Error";
			const message = oError.Message ?? (errorForInfo && (errorForInfo.Message || errorForInfo.message) ? (errorForInfo.Message || errorForInfo.message) : "");
			let notes = oError.Notes;
			if (!notes && errorForInfo && (errorForInfo.Document || errorForInfo.LineNumber)) {
				notes = (errorForInfo.Document ?? "") +
					(errorForInfo.LineNumber ? " line " + errorForInfo.LineNumber : "");
			}

			let formattedError = "<strong>" + type + "</strong>: " + message;
			if (ObjectUtil.HasValue(notes)) {
				formattedError += "<br><br>" + notes;
			}

			UserMessages.DisplayNow(formattedError, "Error");
		} else {
			UserMessages.DisplayNow("Unexpected error.", "Error");
		}

		if (Page && typeof Page.LogError === "function") {
			Page.LogError(errorForInfo);
		}
	},


	HandleSessionExpired: function () {
		UserMessages.Display("Your session has expired and you need to login to continue", "Error");
		Page.Redirect("Login.aspx");
	},

	HandleValidationErrors: function (err) {
		UserMessages.DisplayNow(err.Data.ValidationErrors, "Error");
	},

	Modals: {},

	Grids: new GridCollection(),

	_QueryString: null,

	QueryString: function () {
		if (null == this._QueryString) {
			this._QueryString = UrlUtil.FromQueryString(UrlUtil.GetQueryString(document.location.href), true, true);
			this._QueryString.GetValue = function (sKey, sDefault) {
				sDefault = sDefault || '';
				for (var i in this._QueryString) {
					if (StringUtil.EqualNoCase(i, sKey))
						return this._QueryString[i];
				}

				return sDefault;
			}.bind(this);
		}

		return this._QueryString;
	},

	LoadViewState: function () {
		var oSaved = Page.LocalStorage.get(document.location.href)
		if (null != oSaved) {
			BlindBind(document.body, oSaved);
		}
	},

	SaveViewState: function () {
		Page.LocalStorage.set(document.location.href, BlindUnbind(document.body));
	},

	PreserveViewState: function () {
		Page.AddOnload(Page.LoadViewState);
		Page.AddOnUnload(Page.SaveViewState);
	},

	ClearViewState: function () {
		Page.LocalStorage.set(document.location.href, {});
	},

	LocalStorage: new LocalStorage2(),
	ObjectStorage: new LocalStorage2(),

	LocalSettings: {},

	GetLocalSettings: function () {
		var oSettings = Page.LocalStorage.GetValue("LocalSettings");
		if (null != oSettings) {
			Page.LocalSettings = Object.assign({}, Page.LocalSettings, oSettings);
		}
	},

	SaveLocalSettings: function () {
		Page.LocalStorage.SetValue("LocalSettings", Page.LocalSettings);
	},

	PreserveLocalSettings: function (fCB) {
		Page.AddOnload(
			function () {
				Page.GetLocalSettings();
				if (fCB)
					fCB();
			}
		);
		Page.AddOnUnload(Page.SaveLocalSettings);
	},

	GetSessionID: function () {
		let sessionID = sessionStorage.getItem("sessionID");
		let pageID = StringToInteger(sessionStorage.getItem("pageID"));

		if (null == sessionID) {
			sessionID = StringUtil.UniqueID();
			pageID = 1;

		}

		else {
			let refPageID = localStorage.getItem(sessionID);
			if (refPageID != pageID) //tab duplicated
			{
				console.log('tab duplicated');
				sessionID = StringUtil.UniqueID();
				pageID = 1;
			}
			else {
				pageID += 1;
			}
		}

		sessionStorage.setItem("sessionID", sessionID);
		sessionStorage.setItem("pageID", pageID);
		localStorage.setItem(sessionID, pageID);

		return sessionID;
	}
};

Page.LocalStorage.GetValue = function (sKey) {
	return Page.LocalStorage.get(UrlUtil.GetUrlWithoutParams(document.location.href) + "::" + sKey);
}

Page.LocalStorage.SetValue = function (sKey, oValue) {
	Page.LocalStorage.set(UrlUtil.GetUrlWithoutParams(document.location.href) + "::" + sKey, oValue);
}

Page.ObjectStorage.GetValue = function (sKey) {
	if (typeof ObjectName === "undefined") {
		console.log("ObjectName is not defined");
		return null;
	}

	if (typeof iObjectID === "undefined") {
		console.log("iObjectID is not defined");
		return null;
	}

	return Page.ObjectStorage.get(ObjectName + "::" + iObjectID + "::" + sKey);
}

Page.ObjectStorage.SetValue = function (sKey, oValue) {
	if (typeof ObjectName === "undefined") {
		console.log("ObjectName is not defined");
		return null;
	}

	if (typeof iObjectID === "undefined") {
		console.log("iObjectID is not defined");
		return null;
	}

	Page.ObjectStorage.set(ObjectName + "::" + iObjectID + "::" + sKey, oValue);
}

Page.AddOnLoad = Page.AddOnload;
Page.Refresh = Page.Reload;


Page.AddOnUnload(function () {
	Page.IsUnloading = true;
});

function Redirect(strPage, oQueryParams, strWindow) {
	if (ObjectUtil.HasValue(oQueryParams) && null != oQueryParams) {
		if (/[?]/.test(strPage)) {
			var strQuery = UrlUtil.GetQueryString(strPage);
			var oParams = UrlUtil.FromQueryString(strQuery);
			for (var i in oQueryParams) {
				oParams[i] = oQueryParams[i];
			}

			strPage = UrlUtil.GetUrlWithoutParams(strPage) + "?" + UrlUtil.ToQueryString(oParams);
		}
		else
			strPage += "?" + UrlUtil.ToQueryString(oQueryParams);
	}

	var target = ObjectUtil.HasValue(strWindow) ? strWindow : "_self";

	if (target === "_self") {
		window.location.href = strPage;
		return;
	}

	var w = window.open(strPage, target);

	if (!w || w.closed || typeof w.closed === "undefined") {
		UserMessages.DisplayNow("Popup blocked. Please allow popups and try again.", "Error");		
	}

	try { w.focus(); } catch (e) { }
}


Page.Redirect = Redirect;


/*  Strict-mode friendly rewrite.
	– Keeps the public shape:  Message · CallStack · InnerException
	– No use of arguments.callee / caller / arguments                */

/**
 * Simple exception wrapper that normalises service errors for the UI layer.
 */
class Exception {

	Message = '';
	CallStack = '';
	InnerException = null;      // may hold another Exception/Error

	/** @param {string} message */
	constructor(message = '', inner = null) {

		this.Message = message;
		this.InnerException = inner;

		/* Build a modern stack trace
		   - Error.captureStackTrace is V8 (Chrome / Node).
		   - Fallback to new Error().stack in other engines.          */
		const err = new Error(message);
		if (Error.captureStackTrace) {
			Error.captureStackTrace(err, Exception);      // omit this ctor frame
		}
		this.CallStack = err.stack ?? '';

		// mimic legacy side-effect
		if (typeof Page === 'object' && Page) Page.LastError = this;
	}

	// ---- static helpers --------------------------------------------------

	/** Convert any thrown value into a plain POJO for logging/UI */
	static GetErrorInfo(raw) {

		const info = { RawError: raw };

		// classical Error subclasses
		if (raw instanceof Error) {
			info.Type = raw.constructor.name;     // "ReferenceError", "TypeError", …
			info.Message = raw.message ?? '';
			info.Notes = raw.stack ?? '';
		}

		// our custom Exception keeps the same fields
		if (raw instanceof Exception) {
			info.Message = raw.Message;
			info.Notes = raw.CallStack;
		}

		// plain objects from window.onerror handler
		if (typeof raw === 'object' && raw !== null) {
			if (!info.Message && ObjectUtil.HasValue(raw.Message))
				info.Message = raw.Message;
			if (!info.Type && ObjectUtil.HasValue(raw.ErrorNumber))
				info.Type = 'Error ' + raw.ErrorNumber;

			if (!info.Notes && (ObjectUtil.HasValue(raw.Document) || ObjectUtil.HasValue(raw.LineNumber))) {
				const doc = raw.Document ?? '';
				const line = ObjectUtil.HasValue(raw.LineNumber) ? ' line ' + raw.LineNumber : '';
				info.Notes = (doc + line).trim();
			}
		}

		// recursive inner-exception capture
		if (raw?.InnerException) {
			info.InnerException = Exception.GetErrorInfo(raw.InnerException);
		}

		return info;
	}
}


var ErrorLogging = {
	// Ignore ResizeObserver noise from Chromium (see crbug.com/1266297) so it does not trigger portal level error dialogs.
	IgnoreErrors: ["ResizeObserver loop limit exceeded", "ResizeObserver loop completed with undelivered notifications.", "EvalError: Possible side-effect in debug-evaluate", "Failed to fetch"],

	IsIgnored: function (sError) {

		if (StringUtil.EqualNoCase(sError, "Script error.")) {
			return true;
		}

		for (var i = 0; i < ErrorLogging.IgnoreErrors.length; i++) {
			if (StringUtil.InString(sError, ErrorLogging.IgnoreErrors[i]))
				return true;
		}

		return false;
	},

	LogError: function (oErr) {

		try {
			var oError = Exception.GetErrorInfo(oErr);
			oError.Href = document.location.href;

			var sError = JSON.stringify(oError);
			if (ErrorLogging.IsIgnored(sError))
				return oErr;

			// Try to use sendBeacon to the same endpoint
			if (navigator && typeof navigator.sendBeacon === "function") {

				// Match the JsonMethod payload shape: { Method, Error }
				var oPayload = {
					Method: "ReportError",
					Error: sError
				};

				var sPayload = JSON.stringify(oPayload);
				var blob = new Blob([sPayload], { type: "application/json" });

				var ok = navigator.sendBeacon(Globals.ErrorReportingUrl, blob);

				// If beacon fails, fall back to JsonMethod
				if (!ok) {
					JsonMethod.call(Globals.ErrorReportingUrl, "ReportError", { Error: sError });
				}
			}
			else {
				// No beacon support: existing behavior
				JsonMethod.call(Globals.ErrorReportingUrl, "ReportError", { Error: sError });
			}
		}
		catch (sErr) {
			// Swallow – logging should never throw
		}

		return oErr;
	},

	LogEvent: function (sEvent, sMessage) {
		JsonMethod.call(Globals.ErrorReportingUrl, "ReportEvent", { Event: sEvent, Message: Json.encode(sMessage) });
	}
}

var iErrorCount = 1;
var gUnhandledPromiseReasons = (typeof WeakSet !== "undefined") ? new WeakSet() : null;
if (Globals.ReportErrors) {

	Page.AddOnError(function (oMsg, oDoc, oLine, oCol, oError) {
		try {
			oMsg = oMsg || "";
			oDoc = oDoc || "";
			oLine = oLine || "";

			// Prefer the explicit error argument
			if (oError instanceof Error) {
				Page.LastError = oError;
			}
			// Fallback: some browsers only expose Error via the global event
			else if (typeof window !== "undefined" &&
				window.event &&
				window.event instanceof ErrorEvent &&
				window.event.error instanceof Error) {
				Page.LastError = window.event.error;
			}

			if (iErrorCount > Globals.MaximumErrors) {
				return;
			}

			Page.HandleUnexpectedError({
				ErrorNumber: iErrorCount++,
				Message: oMsg,
				Document: oDoc,
				LineNumber: oLine
			});
		}
		catch (err) {
			// last-ditch; don't rethrow inside onerror
		}
	});

	if (typeof window !== "undefined" && typeof window.addEventListener === "function") {
		window.addEventListener("unhandledrejection", function (event) {
			try {
				if (!event) {
					return;
				}

				var reason = ObjectUtil.HasValue(event.reason) ? event.reason : "Unhandled promise rejection.";
				if (reason instanceof Error) {
					if (gUnhandledPromiseReasons && gUnhandledPromiseReasons.has(reason)) {
						return;
					}
					if (gUnhandledPromiseReasons) {
						gUnhandledPromiseReasons.add(reason);
					}
					Page.LastError = reason;
				}

				Page.HandleUnexpectedError(reason);
				if (typeof event.preventDefault === "function") {
					event.preventDefault();
				}
			}
			catch (err) {
				// last-ditch; don't rethrow inside unhandled rejection handler
			}
		});
	}
}



class SafeElement {
	constructor(selectorOrElement) {
		if (typeof selectorOrElement === 'string') {
			let id = selectorOrElement;
			if (id.startsWith('#')) {
				id = id.substring(1);
			}
			this.element = document.getElementById(id);
		} else {
			// Assumes it's already an element or null
			this.element = selectorOrElement;
		}

		// Your correct tag to identify the wrapper
		this.__isSafeElement = true;
	}

	addClass(className) {
		if (this.element)
			ControlUtil.AddClass(this.element, className);
		return this;
	}

	removeClass(className) {
		if (this.element)
			ControlUtil.RemoveClass(this.element, className);
		return this;
	}

	toggleClass(className, force) {
		// Apply or toggle a class while preserving the fluent SafeElement interface.
		if (this.element) {
			if (ObjectUtil.HasValue(force)) {
				if (force) {
					ControlUtil.AddClass(this.element, className);
				} else {
					ControlUtil.RemoveClass(this.element, className);
				}
			} else if (ControlUtil.HasClass(this.element, className)) {
				ControlUtil.RemoveClass(this.element, className);
			} else {
				ControlUtil.AddClass(this.element, className);
			}
		}
		return this;
	}

	// Convenience wrappers that mirror ControlUtil.Show/Hide while keeping
	// the fluent SafeElement interface intact.

	show() {
		if (this.element)
			ControlUtil.Show(this.element);
		return this;
	}

	hide() {
		if (this.element)
			ControlUtil.Hide(this.element);
		return this;
	}

	setValue(value) {
		if (this.element)
			ControlUtil.SetValue(this.element, value);
		return this;
	}

	setStyle(property, value) {
		// This was already correct, as it accesses the proxied
		// .style property directly.
		if (this.element && this.element.style)
			this.element.style[property] = value;
		return this;
	}

	addEvent(eventName, fn) {
		if (this.element)
			ControlUtil.AddEvent(this.element, eventName, fn);
		return this;
	}

	addClick(fn) {
		if (this.element)
			ControlUtil.AddClick(this.element, fn);
		return this;
	}

	hasClass(className) {
		if (!this.element)
			return false;
		return ControlUtil.HasClass(this.element, className);
	}

	getValue() {
		if (!this.element)
			return undefined;
		return ControlUtil.GetValue(this.element);
	}

	getElement() {
		return this.element;
	}

	parent() {
		// Get the raw parent element
		const parentEl = this.element ? this.element.parentNode : null;

		// This is correct: it returns a *new* proxied wrapper
		// for the parent, allowing chaining.
		return createSafeElement(parentEl);
	}

	getElements(selector) {
		// Guard for null parents so downstream code can safely iterate.
		if (!this.element)
			return [];

		// Reuse the classic ControlUtil selector handling (escapes, fallbacks, etc.).
		const rawElements = ControlUtil.GetChildren(this, selector) || [];

		// Wrap each element so callers can keep chaining SafeElement helpers.
		return rawElements.map(function (el) {
			return createSafeElement(el);
		});
	}

	getText() {
		return ControlUtil.GetText(this.element);
	}

        setText(text) {
                if (this.element)
                        ControlUtil.SetText(this.element, text);
                return this;
        }

        setHtml(html) {
                if (this.element)
                        this.element.innerHTML = (html == null ? "" : html).toString();
                return this;
        }
}

function createSafeElement(selectorOrElement) {

	if (!selectorOrElement) {
		return null;
	}


	if (selectorOrElement && selectorOrElement.__isSafeElement === true) {
		return selectorOrElement;
	}

	const wrapper = new SafeElement(selectorOrElement);

	if (!wrapper.element)
		return null;

	if (typeof Proxy === 'undefined')
		return wrapper;

	return new Proxy(wrapper, {
		get(target, prop, receiver) {

			if (prop in target) {
				return Reflect.get(target, prop, receiver);
			}

			const el = target.element;
			if (!el)
				return undefined;

			const value = el[prop];

			// Methods: bind to the element (`click`, `focus`, etc.)
			if (typeof value === 'function')
				return value.bind(el);

			// Getter-based props (offsetWidth, etc.)
			const proto = Object.getPrototypeOf(el);
			const desc = Object.getOwnPropertyDescriptor(proto, prop);
			if (desc && typeof desc.get === 'function')
				return desc.get.call(el);

			// Fallback: plain property (style, classList, children, etc.)
			return value;
		},
		set(target, prop, value) {
			if (Object.prototype.hasOwnProperty.call(target, prop)) {
				target[prop] = value;
				return true;
			}

			if (target.element)
				target.element[prop] = value;

			return true;
		}
	});
}


function _$(sID) {
	return createSafeElement(sID);
}


/**
 * Array subclass that provides collection helpers for DOM query results.
 */
class ElementList extends Array {
	constructor(elements) {
		// Ensure every entry in the list is wrapped as a SafeElement so callers can
		// use the helper methods (hide, addClass, etc.) directly on each item.
		const wrappedElements = Array.from(elements || []).map(e => createSafeElement(e));
		super(...wrappedElements);
	}

	// Custom method to remove a class from all elements in the list
	removeClass(className) {
		this.forEach(x => x.removeClass(className));
	}

	addClass(className) {
		this.forEach(x => x.addClass(className));
	}
}


function $$(sSelector) {

	// Escape colons in attribute names, e.g. [kcs:FieldName] or [kcs:FieldName=...]
	// We only touch the attribute *name* (before = or ]).
	//Note that we could look at #id:name as well, but we don't currently use that anywhere.
	sSelector = sSelector.replace(/\[([^\]=\s]+)(?=[=\]])/g, function (full, attrName) {
		return "[" + attrName.replace(/:/g, "\\:");
	});


	return new ElementList(document.querySelectorAll(sSelector));
}





/**
 * Primary DOM adapter that mirrors ControlUtil from the classic portal.
 */
var ControlUtil = {

	GetControl: function (oCtrl) {
		// 2. If it's not a wrapper, create a new one.
		// The SafeElement constructor will handle string IDs,
		// raw elements, or null, and all methods are null-safe.
		return createSafeElement(oCtrl);
	},

	AddEvent: function (oCtrl, sEvent, oFunc) {
		var element = ControlUtil.GetControl(oCtrl);

		if (!element)
			return null;

		var eventName = sEvent;
		var handler = oFunc;

		if (StringUtil.InString(sEvent, ":keys")) {
			var mapKeys = {
				down: "ArrowDown",
				up: "ArrowUp",
				enter: "Enter"
			};
			let sKeys = StringUtil.Between(sEvent, "keys(", ")");
			eventName = StringUtil.LeftOfFirst(sEvent, ":");

			let oNestedFunc = oFunc;
			handler = function (event) {
				if (event.key === sKeys || event.key === mapKeys[sKeys]) {
					oNestedFunc(event);
				}
			};
		}

		var addHandler = function (target, evt, fn) {
			if (!target)
				return;

			// SafeElement wrapper → always bind to DOM element
			if (target.__isSafeElement === true) {
				if (target.element && target.element.addEventListener)
					target.element.addEventListener(evt, fn);
				else if (target.element && target.element.attachEvent)
					target.element.attachEvent('on' + evt, fn);
				return;
			}

			// Native DOM element
			if (target.addEventListener) {
				target.addEventListener(evt, fn);
			}
			else if (target.attachEvent) {
				target.attachEvent('on' + evt, fn);
			}
		};

		addHandler(element, eventName, handler);
		return element;
	},

	RemoveEvent: function (oCtrl, sEvent, oFunc) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element)
			return null;

		var eventName = sEvent;
		var handler = oFunc;

		// Handle the special :keys syntax the same way AddEvent does
		if (StringUtil.InString(sEvent, ":keys")) {
			var mapKeys = {
				down: "ArrowDown",
				up: "ArrowUp",
				enter: "Enter"
			};
			let sKeys = StringUtil.Between(sEvent, "keys(", ")");
			eventName = StringUtil.LeftOfFirst(sEvent, ":");
			let oNestedFunc = oFunc;

			// Re-create the exact same wrapper function that AddEvent used
			handler = function (event) {
				if (event.key === sKeys || event.key === mapKeys[sKeys]) {
					oNestedFunc(event);
				}
			};
		}

		var removeHandler = function (target, evt, fn) {
			if (!target)
				return;

			// SafeElement wrapper → use the underlying DOM element
			if (target.__isSafeElement === true) {
				if (target.element && target.element.removeEventListener) {
					target.element.removeEventListener(evt, fn);
				} else if (target.element && target.element.detachEvent) {
					target.element.detachEvent('on' + evt, fn);
				}
				return;
			}

			// Native DOM element
			if (target.removeEventListener) {
				target.removeEventListener(evt, fn);
			} else if (target.detachEvent) {
				target.detachEvent('on' + evt, fn);
			}
		};

		removeHandler(element, eventName, handler);
		return element;
	},

	IsEnterKey: function (event) {
		if (!event)
			return false;

		if (ObjectUtil.HasValue(event.key))
			return event.key === 'Enter';

		return event.keyCode === 13;
	},

	AddClick: function (oCtrl, oFunc) {
		ControlUtil.AddEvent(oCtrl, 'click', oFunc);
	},

	AddChange: function (oCtrl, oFunc) {
		ControlUtil.AddEvent(oCtrl, 'change', oFunc);
	},

	AddOption: function (oDdl, value, sText) {
		let element = ControlUtil.GetControl(oDdl);
		if (!element)
			return null;

		if (element.tagName === 'DATALIST') {
			const option = document.createElement('option');
			option.value = value;
			element.appendChild(option);
		} else if (element.tagName === 'SELECT') {
			const option = document.createElement('option');
			option.value = value;
			option.text = (ObjectUtil.HasValue(sText) ? sText : value);
			element.appendChild(option);
		} else {
			console.error('Unsupported element type. Only <datalist> and <select> are supported.');
		}
	},

	HasClass: function (oCtrl, className) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element || !ObjectUtil.HasValue(className))
			return false;

		return StringUtil.ContainsWord(StringUtil.Clean(element.className), className, ' ');
	},

	AddClass: function (oCtrl, className) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element)
			return null;

		if (!ControlUtil.HasClass(element, className))
			element.className = StringUtil.Clean(element.className + ' ' + className);

		return element;
	},

	/**
	 * Normalises show/hide behaviour so call sites do not repeat the same
	 * class juggling whenever they need to toggle visibility.
	 */
	Show: function (oCtrl) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element)
			return null;

		['Hidden', 'hidden', 'd-none'].forEach(function (className) {
			ControlUtil.RemoveClass(element, className);
		});

		if (element.style)
			element.style.display = '';

		return element;
	},

	Hide: function (oCtrl) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element)
			return null;

		['Hidden'].forEach(function (className) {
			ControlUtil.AddClass(element, className);
		});

		return element;
	},

	RemoveClass: function (oCtrl, className) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element)
			return null;

		element.className = StringUtil.ReplaceAll(element.className, className, '');
		return element;
	},

        /**
         * Adds the shared Bootstrap spinner to a button and disables it.
         * Using this helper ensures every spinner uses the shared markup and CSS hooks.
         */
        ToggleSpinnerOn: function (oCtrl) {
                var button = ControlUtil.GetControl(oCtrl);
                if (!button)
                        return null;

                button.disabled = true;

                var existingSpinner = button.querySelector('.btn-spinner');
                if (existingSpinner)
                        return existingSpinner;

                var spinner = (typeof $$$ === 'function')
                        ? $$$(['span', {
                                'class': 'spinner-border spinner-border-sm ms-1 btn-spinner',
                                'role': 'status',
                                'aria-hidden': 'true'
                        }])
                        : (function () {
                                var fallback = document.createElement('span');
                                fallback.className = 'spinner-border spinner-border-sm ms-1 btn-spinner';
                                fallback.setAttribute('role', 'status');
                                fallback.setAttribute('aria-hidden', 'true');
                                return fallback;
                        })();

                button.appendChild(spinner);
                return spinner;
        },

        /**
         * Removes the shared Bootstrap spinner from a button and re-enables it.
         */
        ToggleSpinnerOff: function (oCtrl) {
                var button = ControlUtil.GetControl(oCtrl);
                if (!button)
                        return null;

                button.disabled = false;
                var toRemove = button.querySelector('.btn-spinner');
                if (toRemove)
                        toRemove.remove();

                return null;
        },

	GetSelected: function (oCtrl) {
		var wrapper = ControlUtil.GetControl(oCtrl);
		var el = (wrapper && wrapper.element) ? wrapper.element : null;

		if (!el || !el.options)
			return [];

		return Array.from(el.options).filter(function (option) {
			return option.selected;
		});
	},

	GetElements: function (oCtrl, sSelector) {
		return ControlUtil.GetChildren(oCtrl, sSelector);
	},

	GetElementText: function (oCtrl) {
		return ControlUtil.GetText(oCtrl);
	},

	Inject: function (oCtrl, oParent) {
		var element = ControlUtil.GetControl(oCtrl);
		var parent = ControlUtil.GetControl(oParent);

		if (element && parent && parent.element)
			parent.element.appendChild(element.element);

		return element;
	},

	InjectBottom: function (oCtrl, oTarget) {
		return ControlUtil.Inject(oCtrl, oTarget);
	},

	InjectTop: function (oCtrl, oTarget) {
		var element = ControlUtil.GetControl(oCtrl);
		var target = ControlUtil.GetControl(oTarget);

		if (element && target && target.element)
			target.element.insertBefore(element.element, target.element.firstChild);

		return element;
	},


	SetStyle: function (oCtrl, property, value) {
		var element = ControlUtil.GetControl(oCtrl);
		if (element && element.style)
			element.style[property] = value;
		return element;
	},

	Disable: function (oCtrl) {
		var wrapper = ControlUtil.GetControl(oCtrl);
		var el = (wrapper && wrapper.element) ? wrapper.element : null;

		if (el)
			el.disabled = true;

		return el;
	},


	GetAttribute: function (oCtrl, sAttr, sDefault) {
		sDefault = (ObjectUtil.HasValue(sDefault) ? sDefault : "");
		var sResult = sDefault;
		var element = ControlUtil.GetControl(oCtrl);

		if (element && element.hasAttribute(sAttr))
			sResult = element.getAttribute(sAttr);

		return sResult;
	},

	GetChildControls: function (oCtrl) {
		var element = ControlUtil.GetControl(oCtrl);
		if (!element)
			return [];

		return element.querySelectorAll("input, select, textarea");
	},

	GetChildren: function (oCtrl, sSelector) {
		var element = ControlUtil.GetControl(oCtrl);

		if (!element)
			return [];

		if (StringUtil.InString(sSelector, ":")) {
			//check if the : is already escaped
			if (!StringUtil.InString(sSelector, "\\:"))
				sSelector = StringUtil.ReplaceAll(sSelector, ":", "\\:");
		}

		if (element.querySelectorAll)
			return Array.from(element.querySelectorAll(sSelector));

		if (StringUtil.StartsWith(sSelector, "."))
			return Array.from(element.getElementsByClassName(StringUtil.RightOfFirst(sSelector, ".")));

		return Array.from(element.getElementsByTagName(sSelector));
	},


	GetValue: function (oCtrl, sDefault) {
		var sResult = sDefault;

		// Always normalize to a SafeElement wrapper, then unwrap to the DOM node.
		var wrapper = ControlUtil.GetControl(oCtrl);
		var el = (wrapper && wrapper.element) ? wrapper.element : null;

		if (el) {
			var tag = el.tagName.toLowerCase();

			if (tag === "input" || tag === "textarea") {
				var type = el.getAttribute("type");

				if (type === "checkbox" || type === "radio") {
					sResult = el.checked;
				}
				else if (StringUtil.EqualNoCase("datetime-local", type)) {
					sResult = el.value;
				}
				// IMPORTANT: use the DOM element's getValue, not the wrapper's
				else if (ObjectUtil.HasValue(el.getValue)) {
					sResult = el.getValue();
				}
				else {
					sResult = el.value;
				}
			}
			else if (tag === "select") {
				if (el.hasAttribute("multiple")) {
					sResult = Array.from(el.options)
						.filter(function (option) { return option.selected; })
						.map(function (option) { return option.value; });
				}
				else {
					sResult = el.value;
				}
			}
			else {
				sResult = el.innerHTML;
			}
		}

		return sResult;
	},


	GetText: function (oCtrl, bRequired, sDefault) {
		if (!ObjectUtil.HasValue(bRequired) || bRequired == null)
			bRequired = false;

		var sResult = sDefault;

		var element = ControlUtil.GetControl(oCtrl);
		if (element) {
			if (["input", "textarea"].includes(element.tagName.toLowerCase())) {
				if (["checkbox", "radio"].includes(element.getAttribute("type")))
					sResult = element.checked;
				else
					sResult = ControlUtil.GetValue(element);
			}
			else if (element.tagName.toLowerCase() == "select") {
				if (element.selectedIndex >= 0)
					sResult = element.options[element.selectedIndex].text;
				else
					sResult = '';
			}
			else
				sResult = element.innerHTML;
		}
		return sResult;
	},

	IsInput: function (oCtrl) {
		var element = ControlUtil.GetControl(oCtrl);
		return (element ? ["input", "select", "textarea"].includes(element.tagName.toLowerCase()) : false);
	},

	SetSelectedText: function (oDdl, sText) {
		var bResult = false;
		oDdl = ControlUtil.GetControl(oDdl);
		if (ObjectUtil.HasValue(oDdl)) {
			sText = ObjectUtil.HasValue(sText) ? sText : "";
			for (var i = 0; i < oDdl.options.length; i++) {
				oDdl.options[i].selected = (StringUtil.EqualNoCase(oDdl.options[i].text, sText));
				if (!bResult)
					bResult = oDdl.options[i].selected;
			}
		}
		return bResult;
	},

	SetValue: function (oCtrl, sValue) {
		var wrapper = ControlUtil.GetControl(oCtrl);
		var el = (wrapper && wrapper.element) ? wrapper.element : null;

		if (!el)
			return;

		if (ObjectUtil.GetType(sValue) === 'object')
			sValue = JSON.stringify(sValue);

		var tag = el.tagName.toLowerCase();

		if (tag === "input" || tag === "textarea") {
			var type = el.getAttribute("type");

			if (type === "checkbox" || type === "radio") {
				el.checked = ObjectUtil.HasValue(sValue) ? StringUtil.ToBoolean(sValue) : false;
			}
			else if (StringUtil.EqualNoCase("image", type)) {
				el.src = ObjectUtil.HasValue(sValue) ? sValue : "";
			}
			else if (StringUtil.EqualNoCase("datetime-local", type)) {
				if (ObjectUtil.HasValue(sValue)) {
					try {
						const d = new Date(sValue);
						d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
						el.value = d.toISOString().slice(0, 16);
					} catch (e) { el.value = null; }
				}
				else {
					el.value = null;
				}
			}
			else if (StringUtil.EqualNoCase("date", type)) {
				if (!StringUtil.IsEmpty(sValue)) {
					try {
						const d = new Date(sValue);
						d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
						el.value = d.toISOString().slice(0, 10);
					} catch (e) { el.value = null; }
				}
				else {
					el.value = null;
				}
			}
			else if (el.setValue) {
				el.setValue(ObjectUtil.HasValue(sValue) ? sValue : "");
			}
			else {
				el.value = ObjectUtil.HasValue(sValue) ? sValue : "";
			}
		}
		else if (tag === "select") {
			if (el.multiple) {
				var lstValues = [];
				if (ObjectUtil.GetType(sValue) === 'array')
					lstValues = sValue;
				else if (ObjectUtil.HasValue(sValue))
					lstValues = [sValue];

				var lstNormalized = lstValues.map(function (value) {
					return ObjectUtil.HasValue(value) ? String(value) : value;
				});

				Array.from(el.options).forEach(function (option) {
					option.selected = (lstNormalized.indexOf(option.value) >= 0);
				});
			}
			else {
				el.value = ObjectUtil.HasValue(sValue) ? sValue : "";
			}
		}
		else {
			el.innerHTML = ObjectUtil.HasValue(sValue) ? sValue : "";
		}

		var event = new Event('change');
		el.dispatchEvent(event);
	},

	SetText : function(sel, value) {
		const el = _$(sel);
		if (el)
			el.textContent = (value == null ? "" : value).toString();
	}


};


var DataUtil = {
	Update: function (oData, oFields) {
		if (ObjectUtil.GetType(oData) == 'string') {
			if (!StringUtil.IsEmpty(oData))
				oData = JSON.parse(oData);  //Json.evaluate with Json.toString was causes tokenizing errors
			else
				oData = {};
		}
		for (var oField in oFields) {
			oData[oField] = oFields[oField];
		}

		return oData;
	},
	Remove: function (oData, Field) {
		if (ObjectUtil.GetType(oData) == 'string') {
			if (!StringUtil.IsEmpty(oData))
				oData = JSON.parse(oData);  //Json.evaluate with Json.toString was causes tokenizing errors
			else
				oData = {};
		}
		delete oData[Field];

		return oData;
	},
	RemoveFields: function (oData, oFields) {
		if (ObjectUtil.GetType(oData) == 'string') {
			if (!StringUtil.IsEmpty(oData))
				oData = JSON.parse(oData);  //Json.evaluate with Json.toString was causes tokenizing errors
			else
				oData = {};
		}
		for (var oField in oFields) {
			fieldName = oFields[oField];
			delete oData[fieldName];
		}

		return oData;
	}
}



/**
 * Helper to get the binding field name, prioritizing the new 'data-bind'
 * attribute but falling back to the legacy 'kcs:FieldName'.
 */
function getBindFieldName(oInput) {
	if (oInput.hasAttribute("data-bind")) {
		return oInput.getAttribute("data-bind");
	}
	return oInput.getAttribute("kcs:FieldName");
}

/**
 * Helper to get the binding field value, prioritizing 'data-bind-value'
 * but falling back to the legacy 'kcs:FieldValue'.
 */
function getBindFieldValue(oInput) {
	if (oInput.hasAttribute("data-bind-value")) {
		return oInput.getAttribute("data-bind-value");
	}
	return oInput.getAttribute("kcs:FieldValue");
}

function BlindUnbindSetField(oObject, sFieldName, sValue, sFieldType) {

	if (StringUtil.InString(sFieldName, ".")) {
		var sSplits = StringUtil.Split(sFieldName, ".");
		var oParentObject = oObject;
		for (var i = 0; i < sSplits.length - 1; i++) {
			var sSplit = sSplits[i];
			if (!ObjectUtil.HasValue(oParentObject[sSplit])) {
				oParentObject[sSplit] = {};
			}
			oParentObject = oParentObject[sSplit];
		}
		oParentObject = BlindUnbindSetField(oParentObject, sSplits.at(-1), sValue);
	}
	else {
		if (ObjectUtil.HasValue(sFieldName) && !StringUtil.IsEmpty(sFieldName)) {
			if (ObjectUtil.HasValue(oObject[sFieldName])) {
				if (ObjectUtil.GetType(oObject[sFieldName]) == "array") {
					oObject[sFieldName].push(sValue);
				}
				else {
					oObject[sFieldName] = [oObject[sFieldName], sValue];
				}
			}
			else {
				if (ObjectUtil.HasValue(sFieldType) && StringUtil.EqualNoCase(sFieldType, "array")) {
					oObject[sFieldName] = [sValue];
				}
				else {
					oObject[sFieldName] = sValue;
				}
			}
		}
	}
	return oObject;
}

/**
 * [REVISED] Unbinds values from form controls into an object.
 * Now supports both 'data-bind' and legacy 'kcs:FieldName'.
 */
function BlindUnbind(oParent) {

	if (ObjectUtil.GetType(oParent) == 'string')
		oParent = _$(oParent);

	var oObject = {};
	oObject.ParentElementID = oParent.id; //used for displaying errors

	oParent.querySelectorAll("[data-bind], [kcs\\:FieldName]").forEach(function (oInput) {
		var sFieldName = getBindFieldName(oInput);
		var sValue;
		var sFieldType = oInput.getAttribute("data-bind-type");
		var sKcsValue = getBindFieldValue(oInput);

		if (["checkbox", "radio"].includes(oInput.getAttribute("type"))) {
			if (oInput.checked) {
				if (ObjectUtil.HasValue(sKcsValue))
					sValue = sKcsValue;
				else
					sValue = true;
			}
			else {
				if (!ObjectUtil.HasValue(sKcsValue))
					sValue = false;
			}
		}
		else if ("image" == oInput.getAttribute("type")) {
			if (ObjectUtil.HasValue(sKcsValue))
				sValue = sKcsValue;
			else
				sValue = oInput.src;
		}
		else {
			if (ObjectUtil.HasValue(sKcsValue))
				sValue = sKcsValue;
			else
				sValue = ControlUtil.GetValue(oInput);
		}

		if (ObjectUtil.HasValue(sValue))
			oObject = BlindUnbindSetField(oObject, sFieldName, sValue, sFieldType);
	});

	return oObject;
}

function BlindBindSetField(oObject, sFieldName, oInput) {

	if (!ObjectUtil.IsDefined(oObject[sFieldName]))
		return oObject;

	var sKcsValue = getBindFieldValue(oInput);

	if (!StringUtil.IsEmpty(sFieldName) && oInput.getAttribute("type") != "button") {
		if (ObjectUtil.GetType(oObject[sFieldName]) == "array") {
			if (["radio", "checkbox"].includes(oInput.getAttribute('type')) && ObjectUtil.HasValue(sKcsValue)) {
				if (oObject[sFieldName].includes(sKcsValue))
					ControlUtil.SetValue(oInput, true);
				else
					ControlUtil.SetValue(oInput, false);
			}
			else if (oInput.tagName.toLowerCase() == "select" && oInput.hasAttribute("multiple")) {
				ControlUtil.SetValue(oInput, oObject[sFieldName]);
			}
			else {
				ControlUtil.SetValue(oInput, oObject[sFieldName][0]);
				oObject[sFieldName].splice(0, 1);
			}
		}
		else {
			if (["radio", "checkbox"].includes(oInput.getAttribute('type')) && ObjectUtil.HasValue(sKcsValue)) {
				if (sKcsValue == oObject[sFieldName])
					ControlUtil.SetValue(oInput, true);
				else
					ControlUtil.SetValue(oInput, false);
			}
			else
				ControlUtil.SetValue(oInput, oObject[sFieldName]);
		}
	}

	return oObject;
}

/**
 * [REVISED] Binds an object to form controls.
 * Now supports both 'data-bind' and legacy 'kcs:FieldName'.
 */
function BlindBind(oParent, oObject) {

	if (ObjectUtil.HasValue(oObject)) {
		if (ObjectUtil.GetType(oParent) == 'string')
			oParent = _$(oParent);

		ControlUtil.GetChildren(oParent, "[data-bind], [kcs\:FieldName]").forEach(oInput => {
			var sFieldName = getBindFieldName(oInput);

			if (StringUtil.IsEmpty(sFieldName))
				return;

			if (StringUtil.InString(sFieldName, ".")) {
				var sSplits = StringUtil.Split(sFieldName, ".");
				var oParentObject = oObject;
				for (var i = 0; i < sSplits.length - 1; i++) {
					var sSplit = sSplits[i];
					if (!ObjectUtil.HasValue(oParentObject[sSplit])) {
						oParentObject[sSplit] = {};
					}
					oParentObject = oParentObject[sSplit];
				}
				BlindBindSetField(oParentObject, sSplits.at(-1), oInput);
			}
			else
				oObject = BlindBindSetField(oObject, sFieldName, oInput);
		});
	}
}


var DebugUtil = {

	JsonToHtml: function (sJson) {
		var object = 'object';
		var sHTML = "";
		try {
			var oVals = (ObjectUtil.GetType(sJson) == 'string' ? Json.evaluate(sJson) : sJson);
			var oTrs = [];
			Object.keys(oVals).forEach(function (oKey) {
				var oVal = oVals[oKey];
				if (['array', 'object'].includes(ObjectUtil.GetType(oVal))) {
					try {
						oVal = DebugUtil.JsonToHtml(oVal);
						//oVal = DebugUtil.JsonToHtml(Json.toString(oVal));
					}
					catch (err) {

					}
				}
				if (ObjectUtil.GetType(oVal) == "string" && StringUtil.StartsWith(oVal, "{") && StringUtil.EndsWith(oVal, "}")) {
					try {
						oVal = DebugUtil.JsonToHtml(oVal);
					}
					catch (err) {

					}
				}
				if (ObjectUtil.GetType(oVal) == "boolean")
					oVal = oVal ? "True" : "False";

				oTrs.push(['tr', ['td', { "class": "InputLabel" }, oKey], ['td', oVal]]);
			});

			//Firefox does not support outerHTML
			document.body.appendChild($$$(['span', { id: 'temp123' }]));
			_$("temp123").appendChild($$$(['table', ['tbody', oTrs]]));
			sHTML = _$("temp123").innerHTML;
			_$("temp123").remove();

		}
		catch (err) {
			sHTML = sJson;
		}

		return sHTML;
	}
};

function $merge(obj1, obj2) {
	let obj3 = {};
	return Object.assign(obj3, obj1, obj2);
}

function toQueryString(obj) {
	return UrlUtil.ToQueryString(obj);
}
