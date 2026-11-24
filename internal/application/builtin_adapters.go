package application

// Import the adapters enabled in this application for their registration side
// effects. Concrete adapters register themselves with adapter.Register, and
// every adapter listed here is initialized without a separate config gate.
import (
	_ "fox-wx-channel/internal/adapter/bilibili"
	_ "fox-wx-channel/internal/adapter/cctv"
	_ "fox-wx-channel/internal/adapter/douyin"
	_ "fox-wx-channel/internal/adapter/kuaishou"
	_ "fox-wx-channel/internal/adapter/singlefile"
	_ "fox-wx-channel/internal/adapter/weibo"
	_ "fox-wx-channel/internal/adapter/wxchannels"
	_ "fox-wx-channel/internal/adapter/wxmp"
	_ "fox-wx-channel/internal/adapter/x"
	_ "fox-wx-channel/internal/adapter/xiaohongshu"
	_ "fox-wx-channel/internal/adapter/youtube"
	_ "fox-wx-channel/internal/adapter/zhihu"
	_ "fox-wx-channel/internal/adapter/instagram"
	// _ "fox-wx-channel/internal/adapter/feishu"
	// _ "fox-wx-channel/internal/adapter/ucdrive"
	// _ "fox-wx-channel/internal/adapter/webpage"
)
