/**
 * KCS Utility Module: stl
 * ------------------------------------------------------------
 * Legacy red-black tree and set/list implementations that underpin a
 * handful of selection workflows (for example GridSelector). Avoid
 * reimplementing data structures and instead reuse these proven, if
 * minimal, helpers when you need ordered sets.
 *
 * Key helpers
 *  - new StlSet(initialValues?)
 *      Backed by the BinaryTree class defined below; exposes insert,
 *      remove, contains, and iteration helpers.
 *  - BinaryTree.walk(callback)
 *      Traverse nodes in-order for reporting/debug output.
 *
 * Usage example – maintain a set of checked row IDs:
 * ```js
 * const selected = new StlSet();
 * selected.insert(rowId);
 * if (!selected.contains(otherId)) {
 *     selected.insert(otherId);
 * }
 * ```
 */

_Red = false; _Black = true;

class _Node {	// tree node
	_Left=null;
	_Parent=null;
	_Right = null;
	_Myval  = null;
	_Color = _Red
}


class BinaryTree {

	_Root=null;

	AllowDuplicates=true;

	isless(oVal1, oVal2) {
		return oVal1 < oVal2;
	}

	contains(oData) {
		return (this.find(oData) != null);
	}

	find(oData) {
		var oCurrent = this._Root;
		while (oCurrent != null) {
			if (this.isless(oData, oCurrent._Myval))
				oCurrent = oCurrent._Left;
			else if (this.isless(oCurrent._Myval, oData))
				oCurrent = oCurrent._Right;
			else
				return oCurrent._Myval;
		}
		return null;
	}

	insert(oData) {
		var oCurrent = this._Root;
		var oParent = null;
		while (oCurrent != null) {
			oParent = oCurrent;
			if (this.isless(oData, oCurrent._Myval))
				oCurrent = oCurrent._Left;
			else if (this.AllowDuplicates == true || this.isless(oCurrent._Myval, oData))
				oCurrent = oCurrent._Right;
			else
				return this;
		}
		oCurrent = new _Node();
		oCurrent._Parent = oParent;
		oCurrent._Myval = oData;
		if (null == oParent)
			this._Root = oCurrent;
		else {
			if (this.isless(oData, oParent._Myval))
				oParent._Left = oCurrent;
			else
				oParent._Right = oCurrent;
		}

		return this;
	}

	_Walk(oNode, fn, bind) {
		if (oNode != null) {
			if (oNode._Left) this._Walk(oNode._Left, fn, bind);
			fn.call(bind, oNode._Myval, this);
			if (oNode._Right) this._Walk(oNode._Right, fn, bind);
		}
	}

	_ReverseWalk(oNode, fn, bind) {
		if (oNode != null) {
			if (oNode._Right) this._ReverseWalk(oNode._Right, fn, bind);
			fn.call(bind, oNode._Myval, this);
			if (oNode._Left) this._ReverseWalk(oNode._Left, fn, bind);
		}
	}

	forEach(fn, bind) {
		this._Walk(this._Root, fn, bind);
	}

	forEachReverse(fn, bind) {
		this._ReverseWalk(this._Root, fn, bind);
	}

	begin() {
		var oCurrent = this._Root;
		var oParent = null;
		while (oCurrent != null) {
			oParent = oCurrent;
			oCurrent = oCurrent._Left;
		}
		return oParent;
	}

	merge(oBinaryTree) {
		if (typeof (oBinaryTree._Root) != false) {
			this._Walk(oBinaryTree._Root, function(oNode) { this.insert(oNode); } .bind(this))
		}
	}

	empty() {
		return (this._Root == null);
	}

	//todo: these could be faster
	length() {
		var i = 0;
		this.forEach(function() {
			i++;
		});
		return i;
	}

	remove(oData) {
		var newSelected = new BinaryTree();
		this.forEach(function(oVal) {
			if (oVal != oData)
				newSelected.insert(oVal);
		});
		this._Root = newSelected._Root;
	}
}

class RedBlackBinaryTree extends BinaryTree {

	_RotateLeft (oNode)
	{
		var oRight = oNode._Right;
		if (null != oRight)
		{
			oNode._Right = oRight._Left;
			if (oRight._Left != null)
				oRight._Left._Parent = oNode;
			oRight._Parent = oNode._Parent;
			if (oNode._Parent == null)
				this._Root = oRight;
			else if (oNode == oNode._Parent._Left)
				oNode._Parent._Left = oRight;
			else
				oNode._Parent._Right = oRight;
			oRight._Left = oNode;
			oNode._Parent = oRight;
		}
	}
	
	_RotateRight (oNode)
	{
		var oLeft = oNode._Left;
		if (null != oLeft)
		{
			oNode._Left = oLeft._Right;
			if (oLeft._Right != null)
				oLeft._Right._Parent = oNode;
			oLeft._Parent = oNode._Parent;
			if (oNode._Parent == null)
				this._Root = oLeft;
			else if (oNode == oNode._Parent._Right)
				oNode._Parent._Right = oLeft;
			else
				oNode._Parent._Left = oLeft;
			oLeft._Right = oNode;
			oNode._Parent = oLeft;
		}
	}
	
	insert (oData)
	{
		var oCurrent = this._Root;
		var oParent = null;
		while (oCurrent != null)
		{
			oParent = oCurrent;
			if (this.isless(oData, oCurrent._Myval))
				oCurrent = oCurrent._Left;
			else if (this.AllowDuplicates == true || this.isless(oCurrent._Myval, oData))
				oCurrent = oCurrent._Right;
			else
				return this;
		}
		oCurrent = new _Node();
		oCurrent._Parent = oParent;
		oCurrent._Myval = oData;
		if (null == oParent)
			this._Root = oCurrent;
		else
		{
			if (this.isless(oData, oParent._Myval))
				oParent._Left = oCurrent;
			else 
				oParent._Right = oCurrent;
		}
		
		oCurrent._Color = _Red;
		while (this._Root != oCurrent && oCurrent._Parent != null && oCurrent._Parent._Color == _Red)
		{
			oParent = oCurrent._Parent;

			//If the parent is the left child
			if (null != oParent._Parent && oParent._Parent._Left == oParent)
			{
				var oNode = oParent._Parent._Right;
				if (oNode != null && oNode._Color == _Red)
				{
					oParent._Color = _Black;
					oNode._Color = _Black;
					oParent._Parent._Color = _Black;
					oCurrent = oParent._Parent;
				}
				else
				{
					if (oCurrent == oParent._Right)
					{
						oCurrent = oParent;
						this._RotateLeft(oCurrent);
					}
					oCurrent._Parent._Color = _Black;
					oCurrent._Parent._Parent._Color = _Black;
					this._RotateRight(oCurrent._Parent._Parent); 
				}
			}
			else
			{
				var oNode = (oParent._Parent != null ? oParent._Parent._Left : null);
				if (oNode != null && oNode._Color == _Red)
				{
					oParent._Color = _Black;
					oNode._Color = _Black;
					if (oParent._Parent != null) oParent._Parent._Color = _Black;
					oCurrent = oParent._Parent;
				}
				else
				{
					if (oCurrent == oParent._Left)
					{
						oCurrent = oParent;
						this._RotateRight(oCurrent);
					}
					if (oCurrent._Parent != null)
					{
						oCurrent._Parent._Color = _Black;
						if (oCurrent._Parent._Parent != null)
						{
							oCurrent._Parent._Parent._Color = _Black;
							this._RotateLeft(oCurrent._Parent._Parent); 
						}
					}
				}
			}				
		}
		
		while (this._Root._Parent != null) this._Root = this._Root._Parent;
		this._Root._Color = _Black;
		
		return this;
	}
}


class StlSet extends RedBlackBinaryTree {
	constructor(oInitialize) {
		super();
		//		//this.parent();
		this.AllowDuplicates = false;
                if (ObjectUtil.HasValue(oInitialize)) {
                        if (ObjectUtil.HasValue(oInitialize.forEach))
				oInitialize.forEach(function(oData) { this.insert(oData); } .bind(this));
			else
				this.insert(oInitialize);
		}
	}

	ToArray() {
		var oArray = [];
		this.forEach(function(oVal) { oArray.push(oVal) })
		return oArray;
	}
}


class StlMap extends RedBlackBinaryTree {

	constructor() {
		super();
		//this.parent();
		this.AllowDuplicates = false;
		this.isless = function (oPair1, oPair2) {
			return (oPair1.Key < oPair2.Key)
		}
	}

	insertPair(oKey, oValue) {
		var oPair = { Key: oKey, Value: oValue };
		this.insert(oPair);
	}

	containsKey(oKey) {
		var oPair = { Key: oKey };
		return this.contains(oPair);
	}

	forEachKey(fn, bind) {
		this.forEach(function (oPair) {
			fn.call(bind, oPair.Key);
		})
	}

	forEachKeyReverse(fn, bind) {
		this.forEachReverse(function (oPair) {
			fn.call(bind, oPair.Key);
		})
	}

	forEachValue(fn, bind) {
		this.forEach(function (oPair) {
			fn.call(bind, oPair.Value);
		})
	}

	forEachValueReverse(fn, bind) {
		this.forEachReverse(function (oPair) {
			fn.call(bind, oPair.Value);
		})
	}

	findValue(oKey) {
		var o = this.find({ Key: oKey });
		return (o != null ? o.Value : null)
	}

	ToJson() {
		var s = "{";

		this.forEach(function (oPair) {
			if (s.length > 1) s += ",";
			s += Json.toString(oPair.Key, false) + ':';
			s += Json.toString(oPair.Value, false);
		});

		s += "}";

		return s;
	}

	removeKey(oData) {
		var newSelected = new Map();
		this.forEach(function (oVal) {
			if (oVal.Key != oData)
				newSelected.insert(oVal);
		});
		this._Root = newSelected._Root;
	}
}

class MultiMap extends StlMap {
	constructor()
	{
		super();
		this.parent();
		this.AllowDuplicates = true;
	}
}
