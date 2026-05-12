class ComputerUseWorkbenchJsonWsServiceService {
  constructor({ baseUrl = "/api/buffaly.openai.computeruse.webharness/computer-use-workbench-json-ws-service", authToken = null } = {}) {
    this.Url = baseUrl;
    this.AuthToken = authToken;
  }
  _validate(oObject, validatorSchema, onValidationErrorCallback) {
    if (oObject == null) oObject = {};
    if (oObject.IsValidated == null || !oObject.IsValidated) {
      if (!Validators.Validate(oObject, validatorSchema)) {
        var oError = { Error: "Invalid data", Data: oObject };
        if (onValidationErrorCallback != null) onValidationErrorCallback(oError);
        else if (Page.HandleValidationErrors) Page.HandleValidationErrors(oError);
        throw "Invalid data";
      }
    }
  }
  GetConfig(request, Callback) { return this._invokeWrap("get-config", "GetConfig", request, ComputerUseWorkbenchJsonWsServiceValidators.GetConfig, Callback); }
  async GetConfigAsync(request) { return await ObjectUtil.Promisify(this, this.GetConfig, [request]); }
  ListRuns(request, Callback) { return this._invokeWrap("list-runs", "ListRuns", request, ComputerUseWorkbenchJsonWsServiceValidators.ListRuns, Callback); }
  async ListRunsAsync(request) { return await ObjectUtil.Promisify(this, this.ListRuns, [request]); }
  GetRun(request, Callback) { return this._invokeWrap("get-run", "GetRun", request, ComputerUseWorkbenchJsonWsServiceValidators.GetRun, Callback); }
  async GetRunAsync(request) { return await ObjectUtil.Promisify(this, this.GetRun, [request]); }
  StartRun(request, Callback) { return this._invokeWrap("start-run", "StartRun", request, ComputerUseWorkbenchJsonWsServiceValidators.StartRun, Callback); }
  async StartRunAsync(request) { return await ObjectUtil.Promisify(this, this.StartRun, [request]); }
  InterruptRun(request, Callback) { return this._invokeWrap("interrupt-run", "InterruptRun", request, ComputerUseWorkbenchJsonWsServiceValidators.InterruptRun, Callback); }
  async InterruptRunAsync(request) { return await ObjectUtil.Promisify(this, this.InterruptRun, [request]); }
  _invokeWrap(kebab, methodName, request, validator, Callback) {
    this._validate(request, validator, null);
    return this._invoke(this.Url + "/" + kebab, methodName, { request: request }, { Serialize: {} }, Callback);
  }
  _invoke(pageUrl, methodName, params, methodConfig, Callback) {
    var initializer = { Page: pageUrl, Method: methodName, Params: params, Serialize: methodConfig.Serialize || {}, onDataReceived: Callback ? function(oRes) { Callback(oRes); } : null, onErrorReceived: (Page.HandleUnexpectedError ? Page.HandleUnexpectedError : null) };
    if (this.AuthToken) initializer.AuthToken = this.AuthToken;
    if (Callback) JsonMethod.callWithInitializer(initializer); else return JsonMethod.callSync(pageUrl, methodName, params, methodConfig.Serialize || {});
  }
}
var ComputerUseWorkbenchJsonWsServiceValidators = {
  GetConfig: { Reserved:{Validators:[Validators.Text],InvalidMessage:"Invalid Reserved",IsOptional:true} },
  ListRuns: { Reserved:{Validators:[Validators.Text],InvalidMessage:"Invalid Reserved",IsOptional:true} },
  GetRun: { RunId:{Validators:[Validators.Text],InvalidMessage:"Invalid RunId"} },
  StartRun: { Direction:{Validators:[Validators.Text],InvalidMessage:"Invalid Direction"}, Model:{Validators:[Validators.Text],InvalidMessage:"Invalid Model",IsOptional:true}, MaxSteps:{Validators:[Validators.Integer],InvalidMessage:"Invalid MaxSteps",IsOptional:true}, MaxStepsPerRun:{Validators:[Validators.Integer],InvalidMessage:"Invalid MaxStepsPerRun",IsOptional:true}, ScreenshotScale:{Validators:[Validators.Number],InvalidMessage:"Invalid ScreenshotScale",IsOptional:true}, CaptureScope:{Validators:[Validators.Text],InvalidMessage:"Invalid CaptureScope",IsOptional:true}, WindowTitle:{Validators:[Validators.Text],InvalidMessage:"Invalid WindowTitle",IsOptional:true}, ProcessName:{Validators:[Validators.Text],InvalidMessage:"Invalid ProcessName",IsOptional:true}, PopupMode:{Validators:[Validators.Text],InvalidMessage:"Invalid PopupMode",IsOptional:true} },
  InterruptRun: { RunId:{Validators:[Validators.Text],InvalidMessage:"Invalid RunId"} }
};
if (typeof ComputerUseWorkbenchJsonWsService === "undefined") { var ComputerUseWorkbenchJsonWsService = new ComputerUseWorkbenchJsonWsServiceService(); }
