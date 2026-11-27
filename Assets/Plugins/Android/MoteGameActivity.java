package com.openframe.mote;

import android.view.View;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import com.unity3d.player.UnityPlayerGameActivity;

public final class MoteGameActivity extends UnityPlayerGameActivity {
    @Override
    protected void onCreateSurfaceView() {
        super.onCreateSurfaceView();
        View content = findViewById(android.R.id.content);
        // Unity 6.6 uses visible insets as padding on the game surface.
        // Pass through keyboard and navigation insets, but draw under the status bar.
        ViewCompat.setOnApplyWindowInsetsListener(content, (view, insets) -> {
            WindowInsetsCompat applied = ViewCompat.onApplyWindowInsets(view, insets);
            return new WindowInsetsCompat.Builder(applied)
                    .setInsets(WindowInsetsCompat.Type.statusBars(), Insets.NONE)
                    .build();
        });
        ViewCompat.requestApplyInsets(content);
    }
}
