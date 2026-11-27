package com.openframe.mote.auth;

import android.app.Activity;
import androidx.annotation.Keep;
import androidx.core.content.ContextCompat;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

@Keep
public final class MoteGoogleLogin {
    @Keep
    public interface Callback {
        void success(String idToken);
        void failure(String reason);
    }

    @Keep
    public static void signIn(Activity activity, String clientId, Callback callback) {
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(new GetSignInWithGoogleOption.Builder(clientId).build())
                .build();
        activity.runOnUiThread(() -> {
            try {
                CredentialManager.create(activity).getCredentialAsync(activity, request, null,
                        ContextCompat.getMainExecutor(activity),
                        new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                            @Override public void onResult(GetCredentialResponse result) {
                                if (!(result.getCredential() instanceof CustomCredential) ||
                                        !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(result.getCredential().getType())) {
                                    callback.failure("UnexpectedCredential");
                                    return;
                                }
                                try {
                                    callback.success(GoogleIdTokenCredential.createFrom(result.getCredential().getData()).getIdToken());
                                } catch (Exception e) {
                                    callback.failure("InvalidCredential");
                                }
                            }
                            @Override public void onError(GetCredentialException e) {
                                callback.failure(e instanceof GetCredentialCancellationException ? "Canceled" : e.getType());
                            }
                        });
            } catch (RuntimeException e) {
                callback.failure(e.getClass().getSimpleName());
            }
        });
    }
}
