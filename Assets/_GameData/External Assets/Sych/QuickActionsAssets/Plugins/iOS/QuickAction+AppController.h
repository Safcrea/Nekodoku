/*
#import "UnityAppController.h"
#import <Foundation/Foundation.h>

//Use QuickAction+AppController for manual initialize quick actions in your override UnityAppController.

@interface UnityAppController (QuickAction)

@property (class, nonatomic, assign, readonly) NSString* performedQuickActionId;
- (BOOL)initialize:(UIApplication*)application withOptions:(NSDictionary*)launchOptions;
- (void)initializePerformAction:(UIApplication *)application shortcutItem:(UIApplicationShortcutItem *)shortcutItem completionHandler:(void (^)(BOOL))completionHandler;

@end
*/