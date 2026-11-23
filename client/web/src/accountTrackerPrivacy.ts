type TrackerRequest = <T>(path: string, body?: Readonly<Record<string, unknown>>) => Promise<T>;
type TextKey = keyof typeof copy;

const endpoint = '/portal/account/tracker/privacy';
const copy = {
  title: ['Activity tracker privacy', 'Quyền riêng tư theo dõi hoạt động'],
  toggle: ['Exclude my account from the public activity tracker', 'Loại trừ tài khoản của tôi khỏi theo dõi hoạt động công khai'],
  unavailable: ['Privacy settings are unavailable. Reopen Security to retry.', 'Chưa đọc được quyền riêng tư. Mở lại mục Bảo mật để thử lại.'],
  saved: ['Privacy preference saved.', 'Đã lưu quyền riêng tư.'],
  failed: ['The change could not be saved. Try again.', 'Chưa lưu được thay đổi. Hãy thử lại.'],
} as const;

export class AccountTrackerPrivacy {
  readonly root = document.createElement('section');
  private readonly title = document.createElement('h3');
  private readonly input = document.createElement('input');
  private readonly label = document.createElement('span');
  private readonly feedback = document.createElement('p');
  private state: boolean | null = null;
  private version = 0;
  private pending = false;
  private submitted: boolean | null = null;
  private message: TextKey | null = null;

  constructor(private readonly request: TrackerRequest, private readonly language: () => 'en' | 'vi') {
    this.root.id = 'accountTrackerPrivacy';
    this.root.className = 'safety-box';
    this.root.hidden = true;
    this.input.type = 'checkbox';
    const label = document.createElement('label');
    label.append(this.input, ' ', this.label);
    this.feedback.setAttribute('role', 'status');
    this.root.append(this.title, label, this.feedback);
    this.input.addEventListener('change', () => { void this.update(this.input.checked); });
  }

  refreshLanguage(authenticated: boolean): void {
    if (!authenticated) this.reset();
    this.render();
  }

  async open(): Promise<void> {
    this.root.hidden = false;
    if (this.state === null && !this.pending) await this.update();
  }

  reset(): void {
    this.version++;
    this.state = null;
    this.pending = false;
    this.submitted = null;
    this.message = null;
    this.root.hidden = true;
    this.render();
  }

  private async update(optedOut?: boolean): Promise<void> {
    const version = ++this.version;
    this.pending = true;
    this.submitted = optedOut ?? null;
    this.message = null;
    this.render();
    try {
      const result = await this.request<{ optedOut: boolean }>(endpoint, optedOut === undefined ? {} : { optedOut });
      if (version !== this.version) return;
      if (typeof result.optedOut !== 'boolean') throw new Error('tracker-privacy-response');
      this.state = result.optedOut;
      this.message = optedOut === undefined ? null : 'saved';
    } catch {
      if (version === this.version) this.message = optedOut === undefined ? 'unavailable' : 'failed';
    } finally {
      if (version === this.version) {
        this.pending = false;
        this.submitted = null;
        this.render();
      }
    }
  }

  private render(): void {
    const index = this.language() === 'vi' ? 1 : 0;
    this.title.textContent = copy.title[index];
    this.label.textContent = copy.toggle[index];
    this.input.disabled = this.state === null || this.pending;
    this.input.checked = (this.submitted ?? this.state) === true;
    this.feedback.textContent = this.message === null ? '' : copy[this.message][index];
  }
}
