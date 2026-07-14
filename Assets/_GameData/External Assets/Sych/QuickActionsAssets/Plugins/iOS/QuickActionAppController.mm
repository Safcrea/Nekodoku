// Comment all this code if you want to use manual initialize Quick Action, uncomment QuickAction+AppController.h and QuickAction+AppController.mm
 
// QuickActionAppController auto initialize quick actions and regiester override UnityAppController.
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

@interface QuickActionAppController : UnityAppController

@property (class, nonatomic, assign, readonly) NSString* performedQuickActionId;

@end

IMPL_APP_CONTROLLER_SUBCLASS(QuickActionAppController)

@implementation QuickActionAppController

static NSString* _performedQuickActionId;

+ (NSString*) performedQuickActionId
{
    return _performedQuickActionId;
}

- (BOOL)application:(UIApplication*)application willFinishLaunchingWithOptions:(NSDictionary*)launchOptions
{
    if (launchOptions[UIApplicationLaunchOptionsShortcutItemKey]) {
        UIApplicationShortcutItem *shortcutItem = launchOptions[UIApplicationLaunchOptionsShortcutItemKey];
        _performedQuickActionId = shortcutItem.type;
    }
    
    return [super application:application willFinishLaunchingWithOptions:launchOptions];
}

- (void)application:(UIApplication *)application performActionForShortcutItem:(UIApplicationShortcutItem *)shortcutItem completionHandler:(void (^)(BOOL))completionHandler {
    
    _performedQuickActionId = shortcutItem.type;
    TriggerQuickActionReceived([_performedQuickActionId UTF8String]);
}

@end

extern "C" const char * getLastPerformedQuickActionId() {
    if (QuickActionAppController.performedQuickActionId != nil) {
        return [QuickActionAppController.performedQuickActionId UTF8String];
    } else {
        return NULL;
    }
}

extern "C" void resetLastPerformedQuickActionId() {
    _performedQuickActionId = nil;
}