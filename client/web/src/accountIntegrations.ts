import { mountTopUpMilestonesWidget, type AccountCredentialsProvider } from './ui/topUpMilestonesWidget.js';
import { mountEventAlertsWidget } from './ui/eventAlertsWidget.js';

/** Keeps existing feature widgets alive when the account dashboard redraws its sections. */
export class AccountIntegrations {
  readonly play = document.getElementById('play')!;
  readonly milestones = document.createElement('section');
  readonly alerts = document.createElement('section');
  private readonly milestoneWidget: ReturnType<typeof mountTopUpMilestonesWidget>;
  private alertWidget: Awaited<ReturnType<typeof mountEventAlertsWidget>> | null = null;
  private openingAlerts: Promise<void> | null = null;

  constructor(private readonly provider: AccountCredentialsProvider, logOut: () => void) {
    this.milestones.id = 'topup-milestones-section';
    this.alerts.id = 'account-event-alerts';
    this.milestoneWidget = mountTopUpMilestonesWidget(this.milestones, provider);
    // Reuse the ticket feature's sign-out control, clearing the whole account session too.
    document.getElementById('playSignOutButton')!.addEventListener('click', logOut);
    this.refreshLanguage();
  }

  refreshLanguage(): void {
    const vi = this.provider.getLanguage?.() === 'vi';
    const hints: Readonly<Record<string, readonly [string, string]>> = {
      playNowButton: ['Create a one-time ticket and enter the game.', 'Tạo vé đăng nhập dùng một lần rồi vào game.'],
      playSignInButton: ['Connect your signed-in account to the game.', 'Kết nối tài khoản đã đăng nhập với game.'],
      playSignOutButton: ['Sign out of this website in this browser.', 'Đăng xuất khỏi website trong trình duyệt này.'],
      playAccount: ['Your game account name.', 'Tên tài khoản game của bạn.'],
      playPassword: ['Your current game password.', 'Mật khẩu tài khoản game hiện tại.'],
    };
    for (const [id, text] of Object.entries(hints)) document.getElementById(id)!.title = text[vi ? 1 : 0];
    this.alertWidget?.refreshLanguage();
  }

  async refresh(section: string): Promise<void> {
    if (section === 'coins') await this.milestoneWidget.refresh();
    if (section !== 'community') return;
    this.openingAlerts ??= mountEventAlertsWidget(this.alerts, () => this.provider.getLanguage?.() ?? 'en').then(widget => {
      this.alertWidget = widget;
      widget.refreshLanguage();
    });
    await this.openingAlerts;
  }
}
