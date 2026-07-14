import Foundation

@_cdecl("addQuickAction")
public func addQuickAction(json: UnsafePointer<CChar>) -> Bool {
    let jsonString = String(cString: json)
    return QuickActions.addQuickAction(json: jsonString)
}

@_cdecl("getAllQuickActions")
public func getAllQuickActions() -> UnsafePointer<CChar>? {
    let cString = strdup(QuickActions.getAllQuickActions())
    return UnsafePointer(cString)
}

@_cdecl("removeQuickAction")
public func removeQuickAction(id: UnsafePointer<CChar>) -> Bool {
    let idString = String(cString: id)
    return QuickActions.removeQuickAction(id: idString)
}

@_cdecl("removeAllQuickActions")
public func removeAllQuickActions() {
    QuickActions.removeAllQuickActions()
}
