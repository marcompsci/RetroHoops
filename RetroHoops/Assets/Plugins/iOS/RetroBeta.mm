// Retro Hoops — TestFlight check (original code). Called from C# via [DllImport("__Internal")].
#import <Foundation/Foundation.h>

extern "C" {

/// 1 when this copy was installed from TestFlight (its App Store receipt is a sandbox receipt).
int RetroBeta_IsTestFlight(void)
{
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    NSURL *url = [[NSBundle mainBundle] appStoreReceiptURL];
#pragma clang diagnostic pop
    return (url != nil && [[url lastPathComponent] isEqualToString:@"sandboxReceipt"]) ? 1 : 0;
}

}
