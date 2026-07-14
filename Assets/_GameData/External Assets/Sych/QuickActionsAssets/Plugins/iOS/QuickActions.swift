import Foundation
import UIKit

public class QuickActions {
    private static let userInfoDataKey = "data"
    private static let userInfoIconKey = "icon"

    public static func addQuickAction(json: String) -> Bool {
        guard let jsonData = json.data(using: .utf8),
              let item = try? JSONDecoder().decode(QuickActionItem.self, from: jsonData) else {
            print("QuickActions: addQuickAction: Error: decoding JSON")
            return false
        }
        
        var userInfoDict: [String: NSSecureCoding] = [userInfoIconKey: item.iconType as NSSecureCoding]
        if let userInfo = item.userInfo {
            userInfoDict[userInfoDataKey] = userInfo as NSSecureCoding
        }
        
        let icon = stringToShortcutIcon(iconType: item.iconType)
        let quickAction = UIApplicationShortcutItem(type: item.id, localizedTitle: item.title, localizedSubtitle: item.subTitle, icon: icon, userInfo: userInfoDict)
      
        var shortcutItems = UIApplication.shared.shortcutItems ?? []
        shortcutItems.append(quickAction)
        UIApplication.shared.shortcutItems = shortcutItems
        return true
    }
    
    static func getAllQuickActions() -> String {
        guard let shortcutItems = UIApplication.shared.shortcutItems else {
            return "[]"
        }
        
        let items = shortcutItems.map { item -> [String: Any] in
            [
                "id": item.type,
                "title": item.localizedTitle,
                "subTitle": item.localizedSubtitle ?? "",
                "iconType": (item.userInfo?[userInfoIconKey] as? String) ?? "",
                "userInfo": (item.userInfo?[userInfoDataKey] as? String) ?? ""
            ]
        }
        
        do {
            let jsonData = try JSONSerialization.data(withJSONObject: items, options: [])
            if let jsonString = String(data: jsonData, encoding: .utf8) {
                return jsonString
            }
        } catch {
            print("QuickActions: getAllQuickActions: Error: serializing JSON")
            return "[]"
        }
        
        return "[]"
    }
    
    public static func removeQuickAction(id: String) -> Bool {
        guard var shortcutItems = UIApplication.shared.shortcutItems, !shortcutItems.isEmpty else {
            return false
        }
        
        let itemsBeforeRemoval = shortcutItems.count
        shortcutItems.removeAll { item -> Bool in
            return item.type == id
        }
        
        let itemsAfterRemoval = shortcutItems.count
        let itemsRemoved = itemsBeforeRemoval != itemsAfterRemoval
        UIApplication.shared.shortcutItems = shortcutItems
        return itemsRemoved
    }
    
    public static func removeAllQuickActions() {
        UIApplication.shared.shortcutItems = nil
    }
    
    private static func stringToShortcutIcon(iconType: String) -> UIApplicationShortcutIcon? {
        switch iconType.lowercased() {
        case "compose":
            return UIApplicationShortcutIcon(type: .compose)
        case "play":
            return UIApplicationShortcutIcon(type: .play)
        case "pause":
            return UIApplicationShortcutIcon(type: .pause)
        case "add":
            return UIApplicationShortcutIcon(type: .add)
        case "location":
            return UIApplicationShortcutIcon(type: .location)
        case "search":
            return UIApplicationShortcutIcon(type: .search)
        case "share":
            return UIApplicationShortcutIcon(type: .share)
        case "prohibit":
            return UIApplicationShortcutIcon(type: .prohibit)
        case "contact":
            return UIApplicationShortcutIcon(type: .contact)
        case "home":
            return UIApplicationShortcutIcon(type: .home)
        case "marklocation":
            return UIApplicationShortcutIcon(type: .markLocation)
        case "favorite":
            return UIApplicationShortcutIcon(type: .favorite)
        case "love":
            return UIApplicationShortcutIcon(type: .love)
        case "cloud":
            return UIApplicationShortcutIcon(type: .cloud)
        case "invitation":
            return UIApplicationShortcutIcon(type: .invitation)
        case "confirmation":
            return UIApplicationShortcutIcon(type: .confirmation)
        case "mail":
            return UIApplicationShortcutIcon(type: .mail)
        case "message":
            return UIApplicationShortcutIcon(type: .message)
        case "date":
            return UIApplicationShortcutIcon(type: .date)
        case "time":
            return UIApplicationShortcutIcon(type: .time)
        case "capturephoto":
            return UIApplicationShortcutIcon(type: .capturePhoto)
        case "capturevideo":
            return UIApplicationShortcutIcon(type: .captureVideo)
        case "task":
            return UIApplicationShortcutIcon(type: .task)
        case "taskcompleted":
            return UIApplicationShortcutIcon(type: .taskCompleted)
        case "alarm":
            return UIApplicationShortcutIcon(type: .alarm)
        case "bookmark":
            return UIApplicationShortcutIcon(type: .bookmark)
        case "shuffle":
            return UIApplicationShortcutIcon(type: .shuffle)
        case "audio":
            return UIApplicationShortcutIcon(type: .audio)
        case "update":
            return UIApplicationShortcutIcon(type: .update)
        default:
            return nil
        }
    }
}

struct QuickActionItem: Codable {
    let id: String
    let title: String
    let subTitle: String
    let iconType: String
    let userInfo: String?

    enum CodingKeys: String, CodingKey {
        case id, title, subTitle, iconType, userInfo
    }
}
