/*
#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*QuickActionCallback)(const char* actionId);

QuickActionCallback quickActionCallback = NULL;

void RegisterQuickActionCallback(QuickActionCallback callback) {
    quickActionCallback = callback;
}

void TriggerQuickActionReceived(const char* actionId) {
    if (quickActionCallback != NULL) {
        quickActionCallback(actionId);
    }
}

#ifdef __cplusplus
}
#endif

#import "UnityAppController.h"

@implementation UnityAppController (QuickAction)

static NSString* _performedQuickActionId;

+ (NSString*) performedQuickActionId
{
    return _performedQuickActionId;
}

- (BOOL)initialize:(UIApplication*)application withOptions:(NSDictionary*)launchOptions
{
    if (launchOptions[UIApplicationLaunchOptionsShortcutItemKey]) {
        UIApplicationShortcutItem *shortcutItem = launchOptions[UIApplicationLaunchOptionsShortcutItemKey];
        _performedQuickActionId = shortcutItem.type;
    }
    
    return YES;
}

- (void)initializePerformAction:(UIApplication *)application shortcutItem:(UIApplicationShortcutItem *)shortcutItem completionHandler:(void (^)(BOOL))completionHandler {
    
    _performedQuickActionId = shortcutItem.type;
    TriggerQuickActionReceived([_performedQuickActionId UTF8String]);
}

@end

extern "C" const char * getLastPerformedQuickActionId() {
    if (UnityAppController.performedQuickActionId != nil) {
        return [UnityAppController.performedQuickActionId UTF8String];
    } else {
        return NULL;
    }
}

extern "C" void resetLastPerformedQuickActionId() {
    _performedQuickActionId = nil;
}
*/